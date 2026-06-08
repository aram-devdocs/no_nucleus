using System.Collections.Generic;
using Nucleus.Core.Command;
using Nucleus.Core.Model;

namespace Nucleus.Game
{
    /// <summary>The seam between Core logic and the game: gathers the roster + fog-of-war threat, runs the pure
    /// brain, and executes the resulting per-unit commands.</summary>
    public sealed class CommanderService : Nucleus.Core.Command.ICampaign
    {
        private readonly CommanderConfig _cfg;
        private readonly GameRoster _roster = new GameRoster();
        private readonly GameIntel _intel = new GameIntel();
        private readonly GameUnitCommands _cmds = new GameUnitCommands();
        private readonly CommanderDebugProbe _debug = new CommanderDebugProbe();
        private CommanderState _auto = new CommanderState();
        private readonly GameProductionService _prodService = new GameProductionService();
        private readonly ProductionQueue _prodQueue = new ProductionQueue();
        private Core.Command.ConvoyCatalog _catalog = new Core.Command.ConvoyCatalog(new List<Core.Command.ConvoyOption>());
        private int _counter;

        public CommanderService(CommanderConfig cfg)
        {
            _cfg = cfg ?? new CommanderConfig();
        }

        public CommanderConfig Config => _cfg;
        /// <summary>Roster from the last Place/Tick (refreshed on the throttled management loop).</summary>
        public IReadOnlyList<UnitView> LastRoster { get; private set; } = new List<UnitView>();

        // Unit-id -> Role map, rebuilt only when LastRoster changes (not per render); AutoHq reuses it for labels.
        private readonly Dictionary<string, Role> _roleMap = new Dictionary<string, Role>();

        private void SetRoster(IReadOnlyList<UnitView> roster)
        {
            LastRoster = roster;
            _roleMap.Clear();
            foreach (var u in roster) _roleMap[u.Id] = u.Role;
        }

        /// <summary>Management tick (throttled by the runtime): refresh the roster + buy menu, then run the brain.</summary>
        public void Tick()
        {
            var roster = _roster.BuildRoster();
            SetRoster(roster);
            _catalog = _prodService.Catalog();

            // The commander is ALWAYS on — without it units idle (the game gives no objectives in this mode).
            var known = _intel.KnownEnemiesNear(new Vec3(0f, 0f, 0f), float.MaxValue);
            _auto.HomeBase = RosterGeometry.Centroid(roster);
            var snapshot = new WorldSnapshot(roster, known, 0f, null, UnityEngine.Time.unscaledTime);
            foreach (var t in CommanderBrain.Tick(snapshot, _auto)) _cmds.Execute(t);

            // Auto-recruit: turn force gaps into convoy buys (within funds) only when Auto-fill is on.
            if (_auto.AiAutoFill && _prodQueue.Pending.Count == 0 && _auto.ProductionNeeds.Count > 0
                && GameManager.GetLocalHQ(out var hq) && hq != null)
            {
                var gap = new Core.Command.Composition();
                foreach (var need in _auto.ProductionNeeds)
                    foreach (var kv in need.Items) gap.Add(kv.Key, kv.Value);
                // EconomyBias -> EconomyWeight: a hoarder commits only a fraction of its funds per cycle
                // (1.0 = spend freely, the stock/default). Clamped so it never exceeds the funds on hand.
                float economy = _auto.Doctrine.EconomyWeight < 0f ? 0f : _auto.Doctrine.EconomyWeight > 1f ? 1f : _auto.Doctrine.EconomyWeight;
                foreach (var opt in ProductionPlanner.Plan(gap, _catalog, hq.factionFunds * economy))
                {
                    _prodQueue.Enqueue(new PurchaseRequest(opt.Name, opt.Cost, null, RoleFamily.Armor, opt.Contents, manual: false));
                    _auto.Log.Append(new ReportEvent(UnityEngine.Time.unscaledTime,
                        ReportKind.ProductionQueued, $"AI buying {opt.Name}", null));
                }
            }

            // Drain the queue every tick (manual buys go through even when the commander is OFF).
            var dispatched = _prodService.Drain(_prodQueue);
            if (dispatched != null)
                _auto.Log.Append(new Core.Command.ReportEvent(0f, Core.Command.ReportKind.ProductionArrived,
                    $"Convoy dispatched: {dispatched.ConvoyName} — arriving at the front"));

            // Publish aircraft ingress zones AFTER the brain runs so they reflect fresh operation phases.
            RefreshAirIntent();
            _debug.Tick();   // optional probe (no-op unless CommanderDebug)
        }

        // Publish aircraft ingress zones (consumed by the NoTarget patch) for operations whose phase engages
        // aircraft, so jets join the auto war while ground holds back for the assault phase.
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

        /// <summary>Render-ready HQ snapshot, with a unit-id→role map so squad rows can show composition.</summary>
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
            _auto.Log.Append(new ReportEvent(UnityEngine.Time.unscaledTime, ReportKind.ObjectiveAdded, $"You set {ObjectiveText.Name(kind)}", null));
            return id;
        }

        // Edit/Move mutate the Objective IN PLACE — the live operation shares the reference, so its tasking
        // follows the change (no stale-position desync).
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
        /// <summary>The cached buyable convoy menu for the build UI (refreshed on the throttled Tick).</summary>
        public Core.Command.ConvoyCatalog BuildCatalog() => _catalog;
        /// <summary>Current faction funds (0 if no HQ).</summary>
        public float Funds() => GameManager.GetLocalHQ(out var hq) && hq != null ? hq.factionFunds : 0f;
        /// <summary>Player queues a convoy buy by name; drains like an AI buy but tagged as yours.</summary>
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
        /// <summary>Take a squad off the AI (Manual) or hand it back (Auto). Manual squads are never tasked by the brain.</summary>
        public void ToggleSquadManual(string squadId)
        {
            var s = _auto.Squads.ById(squadId);
            if (s != null) s.Autonomy = s.Autonomy == AutonomyLevel.Manual ? AutonomyLevel.Auto : AutonomyLevel.Manual;
        }

        /// <summary>Take one operation Manual (AI yields it) or hand it back to Auto; others keep running.</summary>
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
        /// <summary>Save the campaign so a multi-hour war resumes exactly. Transient intel is re-derived, not saved.</summary>
        public void SaveCampaign(string path) => Core.Persistence.CampaignStore.Save(path, _auto);

        /// <summary>Resume a saved campaign, replacing live state. False (no change) if no save exists.</summary>
        public bool LoadCampaign(string path)
        {
            if (!Core.Persistence.CampaignStore.TryLoad(path, out var restored)) return false;
            _auto = restored;
            CommanderPlugin.Log?.LogInfo($"Resumed campaign: {_auto.Objectives.Count} objective(s), {_auto.Operations.Count} operation(s).");
            return true;
        }

    }
}
