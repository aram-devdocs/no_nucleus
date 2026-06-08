using System.Collections.Generic;
using System.Linq;
using Nucleus.Core.Model;

namespace Nucleus.Core.Command
{
    public sealed class BrainConfig
    {
        public float ClusterRadius { get; set; } = 3000f;
        public float CoverageRadius { get; set; } = 4000f;
        public float DefendRadius { get; set; } = 8000f;   // home-defence trigger band; wider than CoverageRadius
        public int MaxSquadsPerOperation { get; set; } = 2;
        public int MaxAutoObjectives { get; set; } = 6;    // caps AUTO objectives only; player drops are uncapped
    }

    /// <summary>Pure decision core of the autonomous commander (no Unity/game refs): turns fog-of-war intel
    /// into objectives and matches squads to them. Pure so the same brain can drive AI + multiplayer.</summary>
    public static class CommanderBrain
    {
        private const float DefendPriority = 50f;   // outranks offensive scores so a threatened base funds first

        /// <summary>One decision tick (pure, mutates <paramref name="state"/>): reconcile squads, generate
        /// objectives, open/advance/prune operations, return the per-unit tasking to issue. Tasks only the
        /// assigned squads' own units (never faction objectives — the stampede trap).</summary>
        public static IReadOnlyList<UnitTask> Tick(WorldSnapshot snapshot, CommanderState state)
        {
            var tasks = new List<UnitTask>();
            state.Squads.Reconcile(snapshot.Roster, snapshot.CommittedUnitIds);   // exclude manual units

            // 1. Advance live operations: drop dead squads, complete when threat is gone, fail when force is lost.
            foreach (var op in state.Operations)
            {
                if (op.IsTerminal) continue;
                op.SquadIds.RemoveAll(sid => state.Squads.ById(sid) == null);
                var current = ThreatNear(snapshot, op.Objective.Position, ResolveRadius(op.Objective.Kind, state.BrainConfig, state.Doctrine));
                if (IsObjectiveResolved(op.Objective.Kind, current))
                {
                    op.Status = OperationStatus.Complete;
                    state.Log.Append(new ReportEvent(snapshot.Time, ReportKind.ObjectiveComplete, CompletionText(op.Objective.Kind), op.Id));
                    continue;
                }
                if (op.SquadIds.Count == 0)
                {
                    op.Status = OperationStatus.Failed;
                    state.Log.Append(new ReportEvent(snapshot.Time, ReportKind.Blocked, $"{ObjectiveText.Name(op.Objective.Kind)}: lost the force", op.Id));
                    continue;
                }
                if (op.Autonomy == AutonomyLevel.Manual) continue;   // player sequences a manual op's phases

                var prevPhase = op.CombatPhase;
                op.CombatPhase = PhaseGates.ActivePhase(current, op.InitialThreat ?? current,
                    new ForceState(FighterStrength(op, state)), state.Doctrine);
                if (op.CombatPhase != prevPhase)
                    state.Log.AppendDistinct(new ReportEvent(snapshot.Time, ReportKind.PhaseChanged,
                        PhaseReason(op.Objective.Kind, op.CombatPhase), op.Id));
            }

            // 2. Free squads from terminal ops, drop those ops, prune auto objectives with no threat + no op.
            foreach (var op in state.Operations)
            {
                if (!op.IsTerminal) continue;
                foreach (var sid in op.SquadIds)
                {
                    var s = state.Squads.ById(sid);
                    if (s != null && s.AssignedOperationId == op.Id) s.AssignedOperationId = null;
                }
            }
            state.Operations.RemoveAll(op => op.IsTerminal);
            state.Objectives.RemoveAll(o => o.Source == ObjectiveSource.Auto
                && state.OperationFor(o.Id) == null
                && !AnyThreatNear(snapshot, o.Position, ResolveRadius(o.Kind, state.BrainConfig, state.Doctrine)));

            // 3. New objectives from enemy clusters (AI-created only). AddAutoObjective re-ids them monotonically;
            //    tick-local generator ids would collide across ticks.
            if (state.AiCreatesObjectives)
            {
                int autoCount = state.Objectives.Count(o => o.Source == ObjectiveSource.Auto);
                // FocusBroad widens/narrows how many objectives the AI juggles at once (ObjectiveSpread 1.0 = stock).
                int effectiveMax = System.Math.Max(1, (int)System.Math.Round(state.BrainConfig.MaxAutoObjectives * state.Doctrine.ObjectiveSpread));
                int room = effectiveMax - autoCount;
                if (room > 0)
                {
                    var planned = new List<Objective>();
                    var defense = GenerateDefense(snapshot, state);
                    if (defense != null) planned.Add(defense);
                    planned.AddRange(GenerateObjectives(snapshot.KnownEnemies, state.Objectives, state.BrainConfig,
                        state.HomeBase, state.Doctrine));
                    foreach (var obj in planned.Take(room)) AddAutoObjective(state, snapshot, obj);
                }
            }

            // 4. AUTO-FILL: open an operation per uncovered objective with a matched force. Off = the human
            //    assigns squads; the brain still advances phases + tasks them below so units never idle.
            var fieldable = new HashSet<string>();
            if (state.AiAutoFill)
            {
                // Priority order, not creation order, so a fresh home defence outbids an older offensive for
                // scarce squads (AssignedOperationId locks a pick for the tick). Id tie-break keeps it deterministic.
                foreach (var obj in state.Objectives.OrderByDescending(o => o.Priority).ThenBy(o => o.Id))
                {
                    if (state.OperationFor(obj.Id) != null) continue;
                    var squadIds = MatchSquads(obj, state.Squads.Squads, state.BrainConfig);
                    if (squadIds.Count == 0) continue; // no force — recruit via ProductionNeeds below
                    fieldable.Add(obj.Id);
                    var initial = ThreatNear(snapshot, obj.Position, ResolveRadius(obj.Kind, state.BrainConfig, state.Doctrine));
                    var op = new Operation(state.NextOperationId(), obj, squadIds)
                    {
                        Status = OperationStatus.Active,
                        InitialThreat = initial
                    };
                    foreach (var sid in squadIds) state.Squads.ById(sid).AssignedOperationId = op.Id;
                    op.CombatPhase = PhaseGates.ActivePhase(initial, initial, new ForceState(FighterStrength(op, state)), state.Doctrine);
                    state.Operations.Add(op);
                    state.Log.Append(new ReportEvent(snapshot.Time, ReportKind.OperationStarted,
                        $"{ObjectiveText.Name(obj.Kind)} {Bearing(state.HomeBase, obj.Position)} — {squadIds.Count} squad{(squadIds.Count == 1 ? "" : "s")} moving in", op.Id));
                }
            }
            else
            {
                foreach (var op in state.Operations) if (!op.IsTerminal) fieldable.Add(op.Objective.Id);
            }

            // 4b. Production needs: an unfielded objective becomes a recruit request (auto-fill only). Per tick.
            //     Bark the block once so the player sees WHY an objective isn't moving (no suitable squad yet).
            state.ProductionNeeds.Clear();
            if (state.AiAutoFill)
                foreach (var obj in state.Objectives)
                    if (state.OperationFor(obj.Id) == null && !fieldable.Contains(obj.Id))
                    {
                        state.ProductionNeeds.Add(RequiredComposition(obj.Kind));
                        state.Log.AppendDistinct(new ReportEvent(snapshot.Time, ReportKind.Blocked,
                            $"{ObjectiveText.Name(obj.Kind)} {Bearing(state.HomeBase, obj.Position)}: no suitable squad — recruiting"));
                    }

            // 5. Issue tasking, but only when a unit's task signature CHANGED — re-spamming SetDestination every
            //    tick fights the game AI.
            var tasked = new HashSet<string>();
            foreach (var op in state.Operations)
            {
                if (op.Status != OperationStatus.Active) continue;
                if (op.Autonomy == AutonomyLevel.Manual) continue;
                // DefendArea holds ground — it skips the offensive phase sequence, so gate its squads by the
                // families that FILL it, not by CombatPhase. Otherwise its squads never match the active phase,
                // zero tasks issue, and the defence is a silent in-game no-op.
                var active = op.Objective.Kind == ObjectiveKind.DefendArea
                    ? Families.SuitableFor(ObjectiveKind.DefendArea)
                    : Families.ActiveInPhase(op.CombatPhase);
                var verb = op.Objective.TargetId != null && op.Objective.Kind == ObjectiveKind.DestroyTarget
                    ? TaskVerb.AttackTarget : TaskVerb.MoveTo;
                // Signature covers id + destination + target + verb so an in-place edit (mutating the shared
                // objective) re-routes committed units instead of being de-duped to the stale point.
                string sig = TaskSignature(op.Objective, verb);
                foreach (var sid in op.SquadIds)
                {
                    var squad = state.Squads.ById(sid);
                    if (squad == null || !active.Contains(squad.Family)) continue; // not this phase's turn
                    if (squad.Autonomy == AutonomyLevel.Manual) continue;
                    foreach (var uid in squad.MemberUnitIds)
                    {
                        tasked.Add(uid);
                        if (state.LastObjectiveByUnit.TryGetValue(uid, out var last) && last == sig) continue;
                        tasks.Add(new UnitTask(uid, verb, op.Objective.Position, op.Objective.TargetId));
                        state.LastObjectiveByUnit[uid] = sig;
                    }
                }
            }
            // Forget units no longer tasked so they re-task cleanly if re-engaged.
            state.PurgeUntaskedMemory(tasked);

            return tasks;
        }

        // Re-ids the planned objective monotonically (tick-local ids collide across ticks) and barks it once.
        private static void AddAutoObjective(CommanderState state, WorldSnapshot snapshot, Objective planned)
        {
            var created = new Objective(state.NextObjectiveId(), planned.Kind, planned.Position,
                planned.Source, planned.TargetId, planned.Priority);
            state.Objectives.Add(created);
            state.Log.Append(new ReportEvent(snapshot.Time, ReportKind.ObjectiveAdded,
                ObjectiveBark(created.Kind, state.HomeBase, created.Position)));
        }

        // Offence/defence resolve when no threat remains; Recon resolves when no fuzzy contact remains. The
        // ground-holding kinds (Capture/ControlAirspace/Resupply) never auto-complete — the prune pass clears them.
        private static bool IsObjectiveResolved(ObjectiveKind kind, ThreatPicture current)
        {
            switch (kind)
            {
                case ObjectiveKind.DestroyTarget:
                case ObjectiveKind.DefendArea:
                    return current.Count == 0;
                case ObjectiveKind.Recon:
                    foreach (var e in current.Enemies) if (!e.Accurate) return false;
                    return true;
                default:
                    return false;
            }
        }

        // Emits one high-priority DefendArea at home when enemies press within DefendRadius and none already
        // covers it. The covered-check uses DefendRadius (not CoverageRadius) because home is a MOVING centroid:
        // the tighter radius would spawn a fresh chase defence each time home drifts, starving the offence (A4).
        private static Objective GenerateDefense(WorldSnapshot snapshot, CommanderState state)
        {
            var home = state.HomeBase;
            if (home.X == 0f && home.Y == 0f && home.Z == 0f) return null;   // home unknown / unset
            if (!AnyThreatNear(snapshot, home, state.BrainConfig.DefendRadius)) return null;
            bool covered = state.Objectives.Any(o => o.Kind == ObjectiveKind.DefendArea
                && o.Position.HorizontalDistanceTo(home) <= state.BrainConfig.DefendRadius);
            if (covered) return null;
            // DefenseBias scales how hard the AI prioritizes home defence over offence (DefendWeight 1.0 = stock).
            return new Objective("auto-def", ObjectiveKind.DefendArea, home, ObjectiveSource.Auto,
                priority: DefendPriority * state.Doctrine.DefendWeight);
        }

        // Fnv1a over the raw float bits, never string.GetHashCode (process-randomized → breaks save/resume).
        private static string TaskSignature(Objective obj, TaskVerb verb)
        {
            var p = obj.Position;
            string raw = obj.Id + "|" + (int)verb + "|"
                + System.BitConverter.SingleToInt32Bits(p.X) + ","
                + System.BitConverter.SingleToInt32Bits(p.Y) + ","
                + System.BitConverter.SingleToInt32Bits(p.Z) + "|"
                + (obj.TargetId ?? "");
            return Fnv1a.Hash(raw).ToString();
        }

        // DefendArea advances/prunes over the same DefendRadius it was raised at; a narrower lens would flap it.
        private static float ResolveRadius(ObjectiveKind kind, BrainConfig cfg, Doctrine doctrine)
            => kind == ObjectiveKind.DefendArea ? cfg.DefendRadius : cfg.CoverageRadius * doctrine.Reach;

        private static ThreatPicture ThreatNear(WorldSnapshot snapshot, Vec3 point, float radius)
        {
            var near = new List<EnemyView>();
            foreach (var e in snapshot.KnownEnemies)
                if (e != null && e.Position.HorizontalDistanceTo(point) <= radius) near.Add(e);
            return new ThreatPicture(near);
        }

        // The combined-arms force an objective kind wants — the gap Production fills.
        private static Composition RequiredComposition(ObjectiveKind kind)
        {
            var c = new Composition();
            switch (kind)
            {
                case ObjectiveKind.DestroyTarget: c.Add(RoleFamily.Armor, 2); c.Add(RoleFamily.Artillery, 1); break;
                case ObjectiveKind.CapturePoint: c.Add(RoleFamily.Armor, 2); c.Add(RoleFamily.Infantry, 1); break;
                case ObjectiveKind.DefendArea: c.Add(RoleFamily.AirDefense, 1); c.Add(RoleFamily.Armor, 1); break;
                case ObjectiveKind.ControlAirspace: c.Add(RoleFamily.AirCombat, 2); break;
                case ObjectiveKind.Resupply: c.Add(RoleFamily.Supply, 1); break;
                // Must match the families MatchSquads fields for Recon, else it re-buys armor forever.
                case ObjectiveKind.Recon: c.Add(RoleFamily.Recon, 1); c.Add(RoleFamily.AirCombat, 1); break;
                default: c.Add(RoleFamily.Armor, 1); break;
            }
            return c;
        }

        private static int FighterStrength(Operation op, CommanderState state)
        {
            int n = 0;
            foreach (var sid in op.SquadIds)
            {
                var s = state.Squads.ById(sid);
                if (s != null && s.Family == RoleFamily.AirCombat) n += s.Strength;
            }
            return n;
        }

        private static bool AnyThreatNear(WorldSnapshot snapshot, Vec3 point, float radius)
        {
            foreach (var e in snapshot.KnownEnemies)
                if (e != null && e.Position.HorizontalDistanceTo(point) <= radius) return true;
            return false;
        }

        // --- Narration ("barks"): plain-language feed lines so the player can read what the AI is doing. ---

        private static readonly string[] Compass = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        // 8-point bearing + km from -> to (X = east, Z = north). Deterministic; no clock/RNG.
        private static string Bearing(Vec3 from, Vec3 to)
        {
            float dx = to.X - from.X, dz = to.Z - from.Z;
            float km = (float)System.Math.Sqrt(dx * dx + dz * dz) / 1000f;
            double ang = System.Math.Atan2(dx, dz) * 180.0 / System.Math.PI; // 0 = north, clockwise
            if (ang < 0) ang += 360;
            int i = (int)System.Math.Round(ang / 45.0) % 8;
            return $"{Compass[i]} {km:0}km";
        }

        private static string ObjectiveBark(ObjectiveKind kind, Vec3 home, Vec3 pos)
        {
            switch (kind)
            {
                case ObjectiveKind.DefendArea: return "AI: defending HQ";
                case ObjectiveKind.CapturePoint: return "AI: capture " + Bearing(home, pos);
                case ObjectiveKind.DestroyTarget: return "AI: strike " + Bearing(home, pos);
                case ObjectiveKind.Recon: return "AI: scout " + Bearing(home, pos);
                case ObjectiveKind.ControlAirspace: return "AI: air patrol " + Bearing(home, pos);
                case ObjectiveKind.Resupply: return "AI: resupply " + Bearing(home, pos);
                default: return "AI: " + kind + " " + Bearing(home, pos);
            }
        }

        private static string CompletionText(ObjectiveKind kind)
        {
            switch (kind)
            {
                case ObjectiveKind.DefendArea: return "Threat cleared at " + ObjectiveText.Name(kind);
                case ObjectiveKind.Recon:      return "Recon complete: " + ObjectiveText.Name(kind);
                default:                       return "Secured: " + ObjectiveText.Name(kind);
            }
        }

        private static string PhaseReason(ObjectiveKind kind, CombatPhase phase)
        {
            string reason;
            switch (phase)
            {
                case CombatPhase.Recon: reason = "scouting"; break;
                case CombatPhase.AirSuperiority: reason = "clearing the skies"; break;
                case CombatPhase.Sead: reason = "suppressing air defenses"; break;
                case CombatPhase.Strike: reason = "softening the target — ground holding"; break;
                case CombatPhase.Assault: reason = "ground assault going in"; break;
                case CombatPhase.Capture: reason = "taking the ground"; break;
                case CombatPhase.Hold: reason = "holding the ground"; break;
                default: reason = phase.ToString(); break;
            }
            return ObjectiveText.Name(kind) + ": " + reason;
        }

        /// <summary>Cluster known enemies (<see cref="ThreatBoard"/>), rank the pockets
        /// (<see cref="TargetPrioritizer"/>), and emit one objective per uncovered pocket at its ranked kind,
        /// priority = score.</summary>
        public static IReadOnlyList<Objective> GenerateObjectives(
            IReadOnlyList<EnemyView> known, IReadOnlyList<Objective> existing, BrainConfig cfg,
            Vec3 homeBase = default(Vec3), Doctrine doctrine = null)
        {
            var result = new List<Objective>();
            var groups = ThreatBoard.Build(known, cfg.ClusterRadius);
            var ranked = TargetPrioritizer.Rank(groups, homeBase, doctrine ?? new Doctrine());
            int idx = 0;

            foreach (var st in ranked)
            {
                var center = st.Group.Center;
                bool covered = existing != null &&
                    existing.Any(o => o.Position.HorizontalDistanceTo(center) <= cfg.CoverageRadius);
                if (covered) continue;

                result.Add(new Objective($"auto-obj-{idx++}", st.SuggestedKind, center,
                    ObjectiveSource.Auto, priority: st.Score));
            }
            return result;
        }

        /// <summary>One free squad per role family the objective needs (so each combat phase has its squad),
        /// strongest-first, deterministic. <paramref name="positions"/> is ignored (call-site compatibility).</summary>
        public static IReadOnlyList<string> MatchSquads(Objective objective, IReadOnlyList<Squad> squads, BrainConfig cfg,
            IReadOnlyDictionary<string, Vec3> positions = null)
        {
            var suitable = Families.SuitableFor(objective.Kind);
            var available = (squads ?? new List<Squad>())
                .Where(s => s != null && !s.IsEmpty && s.AssignedOperationId == null
                    && s.Autonomy != AutonomyLevel.Manual   // never auto-pull a player squad
                    && suitable.Contains(s.Family))
                .ToList();

            var result = new List<string>();
            foreach (var family in suitable.OrderBy(f => f))   // each distinct family once → picks never collide
            {
                var best = available
                    .Where(s => s.Family == family)
                    .OrderByDescending(s => s.Strength).ThenBy(s => s.Id)
                    .FirstOrDefault();
                if (best != null) result.Add(best.Id);
            }
            return result;
        }
    }
}
