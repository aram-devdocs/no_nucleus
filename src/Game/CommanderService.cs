using System.Collections.Generic;
using CommanderLayer.Core.Command;
using CommanderLayer.Core.Model;
using CommanderLayer.Core.Planning;

namespace CommanderLayer.Game
{
    /// <summary>
    /// Orchestrates the commander: gathers the roster + fog-of-war threat, runs the pure planner/manager,
    /// and executes the resulting per-unit commands. This is the seam between Core logic and the game.
    /// </summary>
    public sealed class CommanderService
    {
        private readonly CommanderConfig _cfg;
        private readonly AssignmentManager _mgr;
        private readonly GameRoster _roster = new GameRoster();
        private readonly GameIntel _intel = new GameIntel();
        private readonly GameUnitCommands _cmds = new GameUnitCommands();
        private readonly GameProduction _production = new GameProduction();
        private readonly GameCapture _capture = new GameCapture();
        private readonly CommanderDebugProbe _debug = new CommanderDebugProbe();
        private readonly CommanderState _auto = new CommanderState();
        private readonly GameProductionService _prodService = new GameProductionService();
        private readonly ProductionQueue _prodQueue = new ProductionQueue();
        private int _counter;

        public CommanderService(CommanderConfig cfg)
        {
            _cfg = cfg ?? new CommanderConfig();
            _mgr = new AssignmentManager(_cfg);
        }

        public CommanderConfig Config => _cfg;
        public IReadOnlyList<OrderState> Orders => _mgr.Orders;
        /// <summary>Roster from the last Place/Tick (refreshed on the throttled management loop).</summary>
        public IReadOnlyList<UnitView> LastRoster { get; private set; } = new List<UnitView>();

        // Committed-units snapshot, refreshed on Place/Tick and reused by the per-frame hover preview so we
        // don't rebuild it every frame (review S1).
        private System.Collections.Generic.HashSet<string> _committed = new System.Collections.Generic.HashSet<string>();

        /// <summary>Place an order at a world point: plan a suitable subset and command them. Host-side.</summary>
        public OrderState PlaceOrder(OrderKind kind, Vec3 world, DomainSet domains, float radius)
        {
            // Build = commission-only: queue production at the base; no unit tasking, no rally.
            if (kind == OrderKind.Build)
            {
                string result = _production.Commission();
                var bo = new CommanderOrder("ord-" + (++_counter), kind, world, 0f, domains);
                var bs = new OrderState(bo) { Status = OrderStatus.Complete, Summary = result };
                _mgr.AddExisting(bs);
                Plugin.Log?.LogInfo($"Build commission: {result}");
                return bs;
            }

            var roster = _roster.BuildRoster();
            LastRoster = roster;
            float r = radius > 0f ? radius : _cfg.SelectionRadius;
            var threat = ThreatAssessor.Assess(_intel.KnownEnemiesNear(world, r));
            var order = new CommanderOrder("ord-" + (++_counter), kind, world, r, domains);
            var plan = _mgr.AddOrder(order, roster, threat);
            foreach (var t in plan.Tasks) _cmds.Execute(t);
            _committed = _mgr.CommittedUnitIds(roster);
            RefreshAirIntent();
            Plugin.Log?.LogInfo($"Order {order.Id} ({kind}, {domains}, r={r:0}) at {world}: {plan.Tasks.Count} unit(s) tasked.");
            return _mgr.Orders[_mgr.Orders.Count - 1];
        }

        /// <summary>Live preview of who'd be assigned at a hover point (uses the cached roster).</summary>
        public AssignmentPreview PreviewAt(OrderKind kind, Vec3 world, DomainSet domains, float radius)
        {
            if (LastRoster.Count == 0) LastRoster = _roster.BuildRoster();
            float r = radius > 0f ? radius : _cfg.SelectionRadius;
            var threat = ThreatAssessor.Assess(_intel.KnownEnemiesNear(world, r));
            var order = new CommanderOrder("preview", kind, world, r, domains);
            // Honor cross-order exclusivity using the cached committed snapshot (refreshed on Place/Tick).
            return OrderPlanner.Preview(order, LastRoster, threat, _cfg, _committed);
        }

        /// <summary>Management tick (throttled by the runtime): validate/reassign/complete, re-issue tasks.</summary>
        public void Tick()
        {
            var roster = _roster.BuildRoster();
            LastRoster = roster;
            var reissue = _mgr.Tick(roster,
                o => ThreatAssessor.Assess(_intel.KnownEnemiesNear(o.Position, _cfg.ThreatRadius)),
                o => _capture.IsHeldByUs(o.Position));
            foreach (var t in reissue) _cmds.Execute(t);
            _committed = _mgr.CommittedUnitIds(roster);

            // Autonomous commander (on by default — "do nothing = the side fights"). Generates objectives
            // from fog-of-war intel and tasks auto-formed squads. Coexists with manual orders.
            if (Plugin.EnableAutoCommander)
            {
                var known = _intel.KnownEnemiesNear(new Vec3(0f, 0f, 0f), float.MaxValue); // all tracked enemies
                _auto.HomeBase = ForceCentroid(roster); // proximity reference for target prioritization
                var snapshot = new WorldSnapshot(roster, known, 0f, _committed, UnityEngine.Time.unscaledTime);
                foreach (var t in CommanderBrain.Tick(snapshot, _auto)) _cmds.Execute(t);

                // Auto-production: turn the brain's force gaps into convoy buys (within funds). Plan only
                // when the queue is empty so we don't pile up; the service drains affordable buys each tick.
                if (_prodQueue.Pending.Count == 0 && _auto.ProductionNeeds.Count > 0
                    && GameManager.GetLocalHQ(out var hq) && hq != null)
                {
                    var gap = new Core.Command.Composition();
                    foreach (var need in _auto.ProductionNeeds)
                        foreach (var kv in need.Items) gap.Add(kv.Key, kv.Value);
                    foreach (var opt in ProductionPlanner.Plan(gap, _prodService.Catalog(), hq.factionFunds))
                    {
                        _prodQueue.Enqueue(new PurchaseRequest(opt.Name, opt.Cost, null, RoleFamily.Armor));
                        _auto.Log.Append(new ReportEvent(UnityEngine.Time.unscaledTime,
                            ReportKind.ProductionQueued, $"Buying {opt.Name} ({opt.Cost:0})", null));
                    }
                }
                _prodService.Drain(_prodQueue);
            }

            // Publish aircraft ingress zones AFTER the brain runs so they reflect fresh operation phases.
            RefreshAirIntent();
            _debug.Tick();   // S0 instrumentation (no-op unless CommanderDebug)
        }

        // Publish aircraft ingress zones (consumed by the NoTarget patch) from BOTH layers:
        //  • manual Air-domain orders — SEAD-before-strike: withhold while air defenses remain (ground softens first);
        //  • autonomous operations whose combined-arms phase engages aircraft (recon/air-superiority/SEAD/strike),
        //    so jets join the auto war while ground holds back for the assault phase.
        private void RefreshAirIntent()
        {
            var zones = new List<Vec3>();
            foreach (var o in _mgr.Orders)
            {
                if (o.Status == OrderStatus.Complete || o.Status == OrderStatus.Failed) continue;
                if ((o.Order.Domains & DomainSet.Air) == 0) continue;
                var threat = ThreatAssessor.Assess(_intel.KnownEnemiesNear(o.Order.Position, _cfg.ThreatRadius));
                if (OrderPlanner.SeadPending(o.Order, threat)) continue; // hold aircraft until air defenses fall
                zones.Add(o.Order.Position);
            }
            foreach (var op in _auto.Operations)
            {
                if (op.Status != OperationStatus.Active) continue;
                if (!Families.ActiveInPhase(op.CombatPhase).Contains(RoleFamily.AirCombat)) continue;
                zones.Add(op.Objective.Position);
            }
            AircraftIntent.SetZones(zones);
        }

        // Centroid of the live friendly force — a sensible "home" reference for target prioritization
        // (prefer targets near our own forces). Zero when there's no roster.
        private static Vec3 ForceCentroid(IReadOnlyList<UnitView> roster)
        {
            if (roster == null || roster.Count == 0) return new Vec3(0f, 0f, 0f);
            float x = 0f, y = 0f, z = 0f;
            foreach (var u in roster) { x += u.Position.X; y += u.Position.Y; z += u.Position.Z; }
            return new Vec3(x / roster.Count, y / roster.Count, z / roster.Count);
        }

        /// <summary>Render-ready snapshot of the autonomous commander (ops/squads/production/feed) for the HQ UI.</summary>
        public Core.Command.HqSnapshot AutoHq() => Core.Command.HqView.Build(_auto, _auto.Log, _prodQueue);

        /// <summary>The current commander mode (OFF when the autonomous commander is disabled, else the
        /// commander's autonomy level). Drives the in-panel mode selector — the single source of control.</summary>
        public CommanderMode CurrentMode()
        {
            if (!Plugin.EnableAutoCommander) return CommanderMode.Off;
            return _auto.Autonomy switch
            {
                AutonomyLevel.Assisted => CommanderMode.Assisted,
                AutonomyLevel.Manual => CommanderMode.Manual,
                _ => CommanderMode.Auto,
            };
        }

        /// <summary>Set the commander mode from the panel (replaces the F1 config flag). OFF disables the
        /// autonomous commander entirely; the rest enable it at the matching autonomy level.</summary>
        public void SetMode(CommanderMode mode)
        {
            Plugin.EnableAutoCommander = mode != CommanderMode.Off;
            switch (mode)
            {
                case CommanderMode.Manual: _auto.Autonomy = AutonomyLevel.Manual; break;
                case CommanderMode.Assisted: _auto.Autonomy = AutonomyLevel.Assisted; break;
                case CommanderMode.Auto: _auto.Autonomy = AutonomyLevel.Auto; break;
            }
        }

        /// <summary>Authorise the top Assisted suggestion (the HQ Confirm button). No-op if none pending.</summary>
        public void ConfirmTopProposal()
        {
            if (_auto.Proposals.Count > 0) _auto.ConfirmProposal(_auto.Proposals[0].RefId);
        }

        /// <summary>Take a single operation Manual (AI yields that slice) or hand it back to Auto — the per-op
        /// autonomy control. Other operations keep running on their own.</summary>
        public void ToggleOperationManual(string operationId)
        {
            foreach (var op in _auto.Operations)
                if (op.Id == operationId)
                {
                    op.Autonomy = op.Autonomy == AutonomyLevel.Manual ? AutonomyLevel.Auto : AutonomyLevel.Manual;
                    return;
                }
        }

        public IReadOnlyList<UnitView> CurrentRoster() => _roster.BuildRoster();
        public void Clear(string orderId) => _mgr.Clear(orderId);
        public void ClearAll() => _mgr.ClearAll();
    }
}
