using System.Collections.Generic;
using System.Linq;
using Nucleus.Core.Model;

namespace Nucleus.Core.Command
{
    /// <summary>Brain tunables (autonomous objective generation + force matching).</summary>
    public sealed class BrainConfig
    {
        /// <summary>Known enemies within this distance cluster into one objective.</summary>
        public float ClusterRadius { get; set; } = 3000f;
        /// <summary>An enemy cluster already within this distance of an existing objective is not re-targeted.</summary>
        public float CoverageRadius { get; set; } = 4000f;
        /// <summary>Known enemies within this distance of the home base trigger a DefendArea objective — the AI
        /// reacts to a threat on its base/HQ before the enemy is on top of it (larger than CoverageRadius).</summary>
        public float DefendRadius { get; set; } = 8000f;
        public int MaxSquadsPerOperation { get; set; } = 2;
        /// <summary>The most AUTO objectives the AI keeps active at once — it orchestrates the highest-priority
        /// targets instead of spamming one per enemy cluster across a huge map (which buried the map in markers
        /// and spread squads thin). Player-dropped objectives are NOT counted/capped.</summary>
        public int MaxAutoObjectives { get; set; } = 6;
    }

    /// <summary>
    /// The pure decision core of the autonomous commander — no Unity, no game refs, fully unit-testable.
    /// Turns fog-of-war intel into objectives and matches squads to them. The full per-tick orchestration
    /// (create operations, issue tasking via the executor, advance phases) wires these in P1c/P2; keeping
    /// these as pure functions is what lets the same brain drive AI commanders + multiplayer later.
    /// </summary>
    public static class CommanderBrain
    {
        /// <summary>Priority for an auto-generated home-defense objective — above typical offensive scores so a
        /// threatened base gets force assigned first (defense is funded before attacks within the objective cap).</summary>
        private const float DefendPriority = 50f;

        /// <summary>
        /// One autonomous decision tick (pure): reconcile squads against the live roster, generate objectives
        /// from fog-of-war intel, spin up operations for uncovered objectives with a matched force, prune
        /// terminal/forceless operations, and emit the per-unit tasking to execute. Mutates <paramref
        /// name="state"/>; returns the tasks the executor should issue. Never adds faction objectives (the
        /// stampede trap) — tasking is always the assigned squads' own units. With no enemies + no objectives
        /// it returns nothing, preserving "do nothing = the game still runs".
        /// </summary>
        public static IReadOnlyList<UnitTask> Tick(WorldSnapshot snapshot, CommanderState state)
        {
            var tasks = new List<UnitTask>();
            float coverage = state.BrainConfig.CoverageRadius;
            // Manual-owned units are excluded so the brain and manual orders never task the same unit (S2).
            state.Squads.Reconcile(snapshot.Roster, snapshot.CommittedUnitIds);

            // 1. Advance each live operation: drop dead squads, COMPLETE when its threat is gone, FAIL when it
            //    has lost its whole force.
            foreach (var op in state.Operations)
            {
                if (op.IsTerminal) continue;
                op.SquadIds.RemoveAll(sid => state.Squads.ById(sid) == null);
                var current = ThreatNear(snapshot, op.Objective.Position, coverage);
                if (IsObjectiveResolved(op.Objective.Kind, current))
                {
                    op.Status = OperationStatus.Complete;
                    op.Phase = OrderPhase.Complete;
                    state.Log.Append(new ReportEvent(snapshot.Time, ReportKind.ObjectiveComplete, $"Secured: {op.Objective.Kind}", op.Id));
                    continue;
                }
                if (op.SquadIds.Count == 0)
                {
                    op.Status = OperationStatus.Failed;
                    state.Log.Append(new ReportEvent(snapshot.Time, ReportKind.Blocked, $"{op.Objective.Kind}: lost the force", op.Id));
                    continue;
                }
                // Advance the combined-arms cursor: air superiority -> SEAD -> soften -> assault, per gates.
                // Skip for a player-driven (Manual) operation — the player sequences its phases (autonomy ladder).
                if (op.Autonomy == AutonomyLevel.Manual) continue;
                var prevPhase = op.CombatPhase;
                op.CombatPhase = PhaseGates.ActivePhase(current, op.InitialThreat ?? current,
                    new ForceState(FighterStrength(op, state)), state.Doctrine);
                if (op.CombatPhase != prevPhase)
                    state.Log.AppendDistinct(new ReportEvent(snapshot.Time, ReportKind.PhaseChanged,
                        PhaseReason(op.Objective.Kind, op.CombatPhase), op.Id));
            }

            // 2. Free squads from terminal operations (B1), then drop the terminal ops and prune auto
            //    objectives whose threat is gone and have no live operation (B2 — no unbounded growth, no
            //    squad stuck moving to a dead target).
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
                && !AnyThreatNear(snapshot, o.Position, coverage));

            // 3. New objectives from known enemy clusters — only when the AI is the objective-creator. Each
            //    gets a unique MONOTONIC id (GenerateObjectives' tick-local ids would collide across ticks and
            //    corrupt OperationFor / LastObjectiveByUnit / RemoveObjective).
            if (state.AiCreatesObjectives)
            {
                int autoCount = state.Objectives.Count(o => o.Source == ObjectiveSource.Auto);
                int room = state.BrainConfig.MaxAutoObjectives - autoCount;
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

            // 4. AUTO-FILL: when on, open an operation for each uncovered objective and assign suitable squads —
            //    one per needed family (so each combat phase has its squad), regardless of location (no range).
            //    When off, the human assigns squads (via the service), which opens the operation; the brain
            //    still advances phases + tasks assigned squads below, so units never idle.
            var fieldable = new HashSet<string>();
            if (state.AiAutoFill)
            {
                foreach (var obj in state.Objectives)
                {
                    if (state.OperationFor(obj.Id) != null) continue;
                    var squadIds = MatchSquads(obj, state.Squads.Squads, state.BrainConfig);
                    if (squadIds.Count == 0) continue; // no force available — recruit via ProductionNeeds below
                    fieldable.Add(obj.Id);
                    var initial = ThreatNear(snapshot, obj.Position, coverage); // baseline for the soften gate
                    var op = new Operation(state.NextOperationId(), obj, squadIds)
                    {
                        Status = OperationStatus.Active,
                        InitialThreat = initial
                    };
                    foreach (var sid in squadIds) state.Squads.ById(sid).AssignedOperationId = op.Id;
                    op.CombatPhase = PhaseGates.ActivePhase(initial, initial, new ForceState(FighterStrength(op, state)), state.Doctrine);
                    state.Operations.Add(op);
                    state.Log.Append(new ReportEvent(snapshot.Time, ReportKind.OperationStarted,
                        $"{obj.Kind} {Bearing(state.HomeBase, obj.Position)} — {squadIds.Count} squad{(squadIds.Count == 1 ? "" : "s")} moving in", op.Id));
                }
            }
            else
            {
                foreach (var op in state.Operations) if (!op.IsTerminal) fieldable.Add(op.Objective.Id);
            }

            // 4b. Production needs: an objective with no force becomes a recruit request — only when the AI
            //     auto-fills (the human recruits otherwise). Recomputed each tick.
            state.ProductionNeeds.Clear();
            if (state.AiAutoFill)
                foreach (var obj in state.Objectives)
                    if (state.OperationFor(obj.Id) == null && !fieldable.Contains(obj.Id))
                        state.ProductionNeeds.Add(RequiredComposition(obj.Kind));

            // 5. Issue tasking — only when a unit's target objective CHANGED, so we don't re-spam SetDestination
            //    every tick and fight the game AI (S1).
            var tasked = new HashSet<string>();
            foreach (var op in state.Operations)
            {
                if (op.Status != OperationStatus.Active) continue;
                if (op.Autonomy == AutonomyLevel.Manual) continue; // player drives this slice — brain yields it
                var active = Families.ActiveInPhase(op.CombatPhase); // only this phase's families engage
                var verb = op.Objective.TargetId != null && op.Objective.Kind == ObjectiveKind.DestroyTarget
                    ? TaskVerb.AttackTarget : TaskVerb.MoveTo;
                // De-dup on the full TASK (id + destination + target + verb), not just the objective id — so an
                // in-place Move/EditObjective (which mutates Position/Kind on the shared reference) re-routes the
                // already-committed units instead of being short-circuited and left driving to the old point.
                string sig = TaskSignature(op.Objective, verb);
                foreach (var sid in op.SquadIds)
                {
                    var squad = state.Squads.ById(sid);
                    if (squad == null || !active.Contains(squad.Family)) continue; // not this phase's turn — hold back
                    if (squad.Autonomy == AutonomyLevel.Manual) continue;          // player drives this squad directly
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
            foreach (var k in state.LastObjectiveByUnit.Keys.Where(k => !tasked.Contains(k)).ToList())
                state.LastObjectiveByUnit.Remove(k);

            return tasks;
        }

        /// <summary>Materialize a planned objective into live state with a unique MONOTONIC id (tick-local
        /// generator ids would collide across ticks) and bark the decision once so the player sees the AI think.</summary>
        private static void AddAutoObjective(CommanderState state, WorldSnapshot snapshot, Objective planned)
        {
            var created = new Objective(state.NextObjectiveId(), planned.Kind, planned.Position,
                planned.Source, planned.TargetId, planned.Priority);
            state.Objectives.Add(created);
            state.Log.Append(new ReportEvent(snapshot.Time, ReportKind.ObjectiveAdded,
                ObjectiveBark(created.Kind, state.HomeBase, created.Position)));
        }

        /// <summary>When an operation's objective is "done" and should auto-complete: an offence (DestroyTarget)
        /// or a defence (DefendArea) is done when no threat remains near it; a Recon is done once no low-confidence
        /// contact remains (the intel is resolved). Capture/ControlAirspace/Resupply hold the ground (pruned by
        /// the auto-objective cleanup, not auto-completed here).</summary>
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

        /// <summary>If known enemies are pressing the home base (within <see cref="BrainConfig.DefendRadius"/>) and
        /// no DefendArea already covers it, emit ONE high-priority DefendArea at home so the AI mounts a defence
        /// instead of only ever attacking. Returns null when home is unknown, unthreatened, or already covered.</summary>
        private static Objective GenerateDefense(WorldSnapshot snapshot, CommanderState state)
        {
            var home = state.HomeBase;
            if (home.X == 0f && home.Y == 0f && home.Z == 0f) return null;   // home unknown / unset
            if (!AnyThreatNear(snapshot, home, state.BrainConfig.DefendRadius)) return null;
            bool covered = state.Objectives.Any(o => o.Kind == ObjectiveKind.DefendArea
                && o.Position.HorizontalDistanceTo(home) <= state.BrainConfig.CoverageRadius);
            if (covered) return null;
            return new Objective("auto-def", ObjectiveKind.DefendArea, home, ObjectiveSource.Auto, priority: DefendPriority);
        }

        /// <summary>A deterministic signature of the actual command a unit is being given — objective id +
        /// verb + destination + target. Stored in LastObjectiveByUnit so the per-tick de-dup re-tasks when ANY
        /// of these change (move/retype/retarget), not just when the objective id changes. Uses Fnv1a over the
        /// position's raw float bits — never string.GetHashCode (process-randomized → breaks save/resume).</summary>
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

        private static ThreatPicture ThreatNear(WorldSnapshot snapshot, Vec3 point, float radius)
        {
            var near = new List<EnemyView>();
            foreach (var e in snapshot.KnownEnemies)
                if (e != null && e.Position.HorizontalDistanceTo(point) <= radius) near.Add(e);
            return new ThreatPicture(near);
        }

        /// <summary>A sensible combined-arms force for an objective kind — the gap Production tries to fill.</summary>
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

        // --- Narration ("barks"): plain-language lines so the player can SEE what the AI is doing and why. ---

        private static readonly string[] Compass = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        /// <summary>An 8-point compass bearing + distance in km from <paramref name="from"/> to <paramref name="to"/>
        /// (X = east, Z = north). Deterministic; no clock/RNG.</summary>
        private static string Bearing(Vec3 from, Vec3 to)
        {
            float dx = to.X - from.X, dz = to.Z - from.Z;
            float km = (float)System.Math.Sqrt(dx * dx + dz * dz) / 1000f;
            double ang = System.Math.Atan2(dx, dz) * 180.0 / System.Math.PI; // 0 = north, clockwise
            if (ang < 0) ang += 360;
            int i = (int)System.Math.Round(ang / 45.0) % 8;
            return $"{Compass[i]} {km:0}km";
        }

        /// <summary>One-line "why" for a newly created objective, relative to the home base.</summary>
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

        /// <summary>Why a combat phase is what it is — so the player understands why ground holds back, etc.</summary>
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
            return kind + ": " + reason;
        }

        /// <summary>
        /// Generate objectives from the fog-of-war intel: cluster known enemies into a <see cref="ThreatBoard"/>,
        /// rank the pockets with <see cref="TargetPrioritizer"/> (strategic value + proximity to home, doctrine-
        /// weighted), and emit a DestroyTarget objective at each pocket no existing objective already covers
        /// (priority = the pocket's score, so operations open against the best targets first). One clustering
        /// implementation, shared with the threat board.
        /// </summary>
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

                // Honor the ranker's recommended kind (capture armor/infantry pockets, scout fuzzy contacts,
                // destroy the rest) instead of forcing DestroyTarget — this is what made the AI feel one-note.
                result.Add(new Objective($"auto-obj-{idx++}", st.SuggestedKind, center,
                    ObjectiveSource.Auto, priority: st.Score));
            }
            return result;
        }

        /// <summary>
        /// Pick the squads for an objective: ONE suitable squad per needed role family (so each combat phase —
        /// SEAD, strike, assault, … — has its squad), regardless of where they are on the map (no range). Only
        /// free, non-empty, non-player-owned squads. Strongest-first within a family; deterministic (families
        /// in enum order). The <paramref name="positions"/> param is ignored (kept for call-site compatibility).
        /// </summary>
        public static IReadOnlyList<string> MatchSquads(Objective objective, IReadOnlyList<Squad> squads, BrainConfig cfg,
            IReadOnlyDictionary<string, Vec3> positions = null)
        {
            var suitable = Families.SuitableFor(objective.Kind);
            var available = (squads ?? new List<Squad>())
                .Where(s => s != null && !s.IsEmpty && s.AssignedOperationId == null
                    && s.Autonomy != AutonomyLevel.Manual // player-owned squad — never auto-pulled into an op
                    && suitable.Contains(s.Family))
                .ToList();

            var result = new List<string>();
            foreach (var family in suitable.OrderBy(f => f))      // deterministic per-family coverage
            {
                var best = available
                    .Where(s => s.Family == family && !result.Contains(s.Id))
                    .OrderByDescending(s => s.Strength).ThenBy(s => s.Id)
                    .FirstOrDefault();
                if (best != null) result.Add(best.Id);
            }
            return result;
        }
    }
}
