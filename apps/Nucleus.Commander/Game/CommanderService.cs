using System.Collections.Generic;
using Nucleus.Core.Command;
using Nucleus.Core.Model;
using Nucleus.Core.Planning;

namespace Nucleus.Game
{
    /// <summary>
    /// Orchestrates the commander: gathers the roster + fog-of-war threat, runs the pure planner/manager,
    /// and executes the resulting per-unit commands. This is the seam between Core logic and the game.
    /// </summary>
    public sealed class CommanderService : Nucleus.Core.Command.ICampaign
    {
        private readonly CommanderConfig _cfg;
        private readonly AssignmentManager _mgr;
        private readonly GameRoster _roster = new GameRoster();
        private readonly GameIntel _intel = new GameIntel();
        private readonly GameUnitCommands _cmds = new GameUnitCommands();
        private readonly GameCapture _capture = new GameCapture();
        private readonly CommanderDebugProbe _debug = new CommanderDebugProbe();
        private CommanderState _auto = new CommanderState();
        private readonly GameProductionService _prodService = new GameProductionService();
        private readonly ProductionQueue _prodQueue = new ProductionQueue();
        private Core.Command.ConvoyCatalog _catalog = new Core.Command.ConvoyCatalog(new List<Core.Command.ConvoyOption>());
        private int _counter;

        public CommanderService(CommanderConfig cfg)
        {
            _cfg = cfg ?? new CommanderConfig();
            _mgr = new AssignmentManager(_cfg);
        }

        public CommanderConfig Config => _cfg;
        /// <summary>Roster from the last Place/Tick (refreshed on the throttled management loop).</summary>
        public IReadOnlyList<UnitView> LastRoster { get; private set; } = new List<UnitView>();

        // Unit-id -> Role map, rebuilt only when LastRoster changes (the throttled 3s tick) rather than on every
        // render (~7Hz commander, ~2Hz HUD, and the WAR panel) — AutoHq reuses it for squad composition labels.
        private readonly Dictionary<string, Role> _roleMap = new Dictionary<string, Role>();

        private void SetRoster(IReadOnlyList<UnitView> roster)
        {
            LastRoster = roster;
            _roleMap.Clear();
            foreach (var u in roster) _roleMap[u.Id] = u.Role;
        }

        // Committed-units snapshot, refreshed on Place/Tick and reused by the per-frame hover preview so we
        // don't rebuild it every frame (review S1).
        private System.Collections.Generic.HashSet<string> _committed = new System.Collections.Generic.HashSet<string>();

        /// <summary>Management tick (throttled by the runtime): validate/reassign/complete, re-issue tasks.</summary>
        public void Tick()
        {
            var roster = _roster.BuildRoster();
            SetRoster(roster);
            _catalog = _prodService.Catalog(); // refresh the buy menu once per (throttled) tick, not per frame
            var reissue = _mgr.Tick(roster,
                o => ThreatAssessor.Assess(_intel.KnownEnemiesNear(o.Position, _cfg.ThreatRadius)),
                o => _capture.IsHeldByUs(o.Position));
            foreach (var t in reissue) _cmds.Execute(t);
            _committed = _mgr.CommittedUnitIds(roster);

            // The commander is ALWAYS on — without it, units idle (the game gives no objectives in this mode).
            // The brain forms squads, advances operations, and tasks them; the two toggles inside it gate
            // objective generation (AiCreatesObjectives) and squad assignment/recruit (AiAutoFill).
            var known = _intel.KnownEnemiesNear(new Vec3(0f, 0f, 0f), float.MaxValue); // all tracked enemies
            _auto.HomeBase = RosterGeometry.Centroid(roster);
            var snapshot = new WorldSnapshot(roster, known, 0f, _committed, UnityEngine.Time.unscaledTime);
            foreach (var t in CommanderBrain.Tick(snapshot, _auto)) _cmds.Execute(t);

            // Auto-recruit: turn force gaps into convoy buys (within funds) only when Auto-fill is on.
            if (_auto.AiAutoFill && _prodQueue.Pending.Count == 0 && _auto.ProductionNeeds.Count > 0
                && GameManager.GetLocalHQ(out var hq) && hq != null)
            {
                var gap = new Core.Command.Composition();
                foreach (var need in _auto.ProductionNeeds)
                    foreach (var kv in need.Items) gap.Add(kv.Key, kv.Value);
                foreach (var opt in ProductionPlanner.Plan(gap, _catalog, hq.factionFunds))
                {
                    _prodQueue.Enqueue(new PurchaseRequest(opt.Name, opt.Cost, null, RoleFamily.Armor, opt.Contents, manual: false));
                    _auto.Log.Append(new ReportEvent(UnityEngine.Time.unscaledTime,
                        ReportKind.ProductionQueued, $"AI buying {opt.Name}", null));
                }
            }

            // Drain the production queue every tick (manual buys go through even when the commander is OFF).
            // Announce a dispatched convoy on the feed so the player sees their purchase take effect.
            var dispatched = _prodService.Drain(_prodQueue);
            if (dispatched != null)
                _auto.Log.Append(new Core.Command.ReportEvent(0f, Core.Command.ReportKind.ProductionArrived,
                    $"Convoy dispatched: {dispatched.ConvoyName} — arriving at the front"));

            // Publish aircraft ingress zones AFTER the brain runs so they reflect fresh operation phases.
            RefreshAirIntent();
            _debug.Tick();   // S0 instrumentation (no-op unless CommanderDebug)
        }

        // Publish aircraft ingress zones (consumed by the NoTarget patch) from the autonomous operations whose
        // combined-arms phase engages aircraft (recon/air-superiority/SEAD/strike), so jets join the auto war
        // while ground holds back for the assault phase.
        private void RefreshAirIntent()
        {
            var zones = new List<Vec3>();
            foreach (var op in _auto.Operations)
            {
                if (op.Status != OperationStatus.Active) continue;
                if (!Families.ActiveInPhase(op.CombatPhase).Contains(RoleFamily.AirCombat)) continue;
                zones.Add(op.Objective.Position);
            }
            AircraftIntent.SetZones(zones);
        }

        /// <summary>Render-ready snapshot of the autonomous commander (ops/squads/production/feed) for the HQ UI.
        /// Passes a unit-id→role map (from the live roster) so squad rows can show composition ("2× MBT, 1× IFV").</summary>
        public Core.Command.HqSnapshot AutoHq()
        {
            return Core.Command.HqView.Build(_auto, _auto.Log, _prodQueue, 10, _roleMap);
        }

        // ---- ICampaign aliases (the shared-campaign contract the host exposes to every mod) ----
        public Core.Command.HqSnapshot Hq() => AutoHq();
        public Core.Command.ConvoyCatalog Catalog() => BuildCatalog();

        // ---- the two command toggles ----
        public bool AiCreatesObjectives => _auto.AiCreatesObjectives;
        public bool AiAutoFill => _auto.AiAutoFill;
        public void SetAiCreatesObjectives(bool on) => _auto.AiCreatesObjectives = on;
        public void SetAiAutoFill(bool on) => _auto.AiAutoFill = on;

        // ---- objectives (the single command primitive) ----
        public string CreateObjective(ObjectiveKind kind, Vec3 world, string targetId = null)
        {
            var id = "obj-" + (++_counter);
            _auto.Objectives.Add(new Objective(id, kind, world, ObjectiveSource.Player, targetId, priority: 5f));
            _auto.Log.Append(new ReportEvent(UnityEngine.Time.unscaledTime, ReportKind.ObjectiveAdded, $"You set {kind}", null));
            return id;
        }

        // Edit/Move mutate the Objective IN PLACE — the live operation shares the reference, so its tasking,
        // completion and phase logic follow the change (no stale-position desync).
        public void EditObjective(string id, ObjectiveKind? kind = null, float? priority = null)
        {
            var o = _auto.Objectives.Find(x => x.Id == id);
            if (o == null) return;
            if (priority.HasValue) o.Priority = priority.Value;
            if (kind.HasValue) o.Kind = kind.Value;
        }

        public void MoveObjective(string id, Vec3 world)
        {
            var o = _auto.Objectives.Find(x => x.Id == id);
            if (o != null) o.Position = world;
        }

        public void RemoveObjective(string id)
        {
            _auto.Objectives.RemoveAll(o => o.Id == id);
            var op = _auto.OperationFor(id);
            if (op != null) op.Status = OperationStatus.Failed; // next tick frees its squads + prunes it
        }

        /// <summary>Assign a squad to an objective (the human-driven path when AI Auto-fill is off): open or
        /// extend that objective's operation with the squad. The brain then advances its phases + tasks it.</summary>
        public void AssignSquad(string objectiveId, string squadId)
        {
            var obj = _auto.Objectives.Find(o => o.Id == objectiveId);
            var squad = _auto.Squads.ById(squadId);
            if (obj == null || squad == null) return;
            var op = _auto.OperationFor(objectiveId);
            if (op == null)
            {
                op = new Operation(_auto.NextOperationId(), obj, new[] { squadId }) { Status = OperationStatus.Active };
                _auto.Operations.Add(op);
            }
            else if (!op.SquadIds.Contains(squadId)) op.SquadIds.Add(squadId);
            squad.AssignedOperationId = op.Id;
        }

        // ---- Manual production (buy troops) ----
        /// <summary>The buyable convoy menu (name + cost + real contents) for the build UI. Cached: refreshed
        /// on the throttled Tick, NOT rebuilt every frame.</summary>
        public Core.Command.ConvoyCatalog BuildCatalog() => _catalog;
        /// <summary>Current faction funds (0 if no HQ) so the build UI can grey out unaffordable buys.</summary>
        public float Funds() => GameManager.GetLocalHQ(out var hq) && hq != null ? hq.factionFunds : 0f;
        /// <summary>Player queues a convoy buy by name; it drains (when affordable) like an AI buy but is
        /// tagged as yours. Works in any mode.</summary>
        public void BuyConvoy(string name)
        {
            foreach (var o in _prodService.Catalog().Options)
                if (o.Name == name)
                {
                    _prodQueue.Enqueue(new PurchaseRequest(o.Name, o.Cost, null, RoleFamily.Armor, o.Contents, manual: true));
                    _auto.Log.Append(new ReportEvent(UnityEngine.Time.unscaledTime, ReportKind.ProductionQueued, $"You queued {o.Name}", null));
                    return;
                }
        }

        // ---- Squad management ----
        /// <summary>Take a single squad off the AI (Manual) or hand it back (Auto) — the player owns those
        /// units while Manual (brain never tasks or re-assigns them).</summary>
        public void ToggleSquadManual(string squadId)
        {
            var s = _auto.Squads.ById(squadId);
            if (s != null) s.Autonomy = s.Autonomy == AutonomyLevel.Manual ? AutonomyLevel.Auto : AutonomyLevel.Manual;
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

        // ---- Campaign persistence (save / resume) ----
        /// <summary>Save the autonomous campaign — objectives, operations, squads, doctrine, autonomy and the
        /// id counters — to disk so a multi-hour war can be resumed exactly. Transient per-tick intel is not
        /// saved (it is re-derived from the live game next tick). See <see cref="Core.Persistence.CampaignStore"/>.</summary>
        public void SaveCampaign(string path) => Core.Persistence.CampaignStore.Save(path, _auto);

        /// <summary>Resume a saved campaign from disk, replacing the live autonomous state. Returns false (and
        /// changes nothing) if no save exists at <paramref name="path"/>.</summary>
        public bool LoadCampaign(string path)
        {
            if (!Core.Persistence.CampaignStore.TryLoad(path, out var restored)) return false;
            _auto = restored;
            CommanderPlugin.Log?.LogInfo($"Resumed campaign: {_auto.Objectives.Count} objective(s), {_auto.Operations.Count} operation(s).");
            return true;
        }

    }
}
