using System.Collections.Generic;
using System.Linq;
using CommanderLayer.Core.Model;

namespace CommanderLayer.Core.Command
{
    /// <summary>Brain tunables (autonomous objective generation + force matching).</summary>
    public sealed class BrainConfig
    {
        /// <summary>Known enemies within this distance cluster into one objective.</summary>
        public float ClusterRadius { get; set; } = 3000f;
        /// <summary>An enemy cluster already within this distance of an existing objective is not re-targeted.</summary>
        public float CoverageRadius { get; set; } = 4000f;
        public int MaxSquadsPerOperation { get; set; } = 2;
    }

    /// <summary>
    /// The pure decision core of the autonomous commander — no Unity, no game refs, fully unit-testable.
    /// Turns fog-of-war intel into objectives and matches squads to them. The full per-tick orchestration
    /// (create operations, issue tasking via the executor, advance phases) wires these in P1c/P2; keeping
    /// these as pure functions is what lets the same brain drive AI commanders + multiplayer later.
    /// </summary>
    public static class CommanderBrain
    {
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
            if (state.Autonomy == AutonomyLevel.Manual) return tasks; // player took the whole commander

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
                if (current.Count == 0 && op.Objective.Kind == ObjectiveKind.DestroyTarget)
                {
                    op.Status = OperationStatus.Complete;
                    op.Phase = OrderPhase.Complete;
                    continue;
                }
                if (op.SquadIds.Count == 0) { op.Status = OperationStatus.Failed; continue; }
                // Advance the combined-arms cursor: air superiority -> SEAD -> soften -> assault, per gates.
                op.CombatPhase = PhaseGates.ActivePhase(current, op.InitialThreat ?? current,
                    new ForceState(FighterStrength(op, state)), state.Doctrine);
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

            // 3. New objectives from known enemy clusters not already covered.
            foreach (var obj in GenerateObjectives(snapshot.KnownEnemies, state.Objectives, state.BrainConfig))
                state.Objectives.Add(obj);

            // 4. Open an operation for each uncovered objective, matching a suitable free force.
            foreach (var obj in state.Objectives)
            {
                if (state.OperationFor(obj.Id) != null) continue;
                var squadIds = MatchSquads(obj, state.Squads.Squads, state.BrainConfig);
                if (squadIds.Count == 0) continue; // no force available — production request comes in P3
                var initial = ThreatNear(snapshot, obj.Position, coverage); // baseline for the soften gate
                var op = new Operation(state.NextOperationId(), obj, squadIds)
                {
                    Status = OperationStatus.Active,
                    InitialThreat = initial
                };
                foreach (var sid in squadIds) state.Squads.ById(sid).AssignedOperationId = op.Id;
                // Set the starting phase now so the op tasks the right families on its very first tick.
                op.CombatPhase = PhaseGates.ActivePhase(initial, initial, new ForceState(FighterStrength(op, state)), state.Doctrine);
                state.Operations.Add(op);
            }

            // 5. Issue tasking — only when a unit's target objective CHANGED, so we don't re-spam SetDestination
            //    every tick and fight the game AI (S1).
            var tasked = new HashSet<string>();
            foreach (var op in state.Operations)
            {
                if (op.Status != OperationStatus.Active) continue;
                var active = Families.ActiveInPhase(op.CombatPhase); // only this phase's families engage
                var verb = op.Objective.TargetId != null && op.Objective.Kind == ObjectiveKind.DestroyTarget
                    ? TaskVerb.AttackTarget : TaskVerb.MoveTo;
                foreach (var sid in op.SquadIds)
                {
                    var squad = state.Squads.ById(sid);
                    if (squad == null || !active.Contains(squad.Family)) continue; // not this phase's turn — hold back
                    foreach (var uid in squad.MemberUnitIds)
                    {
                        tasked.Add(uid);
                        if (state.LastObjectiveByUnit.TryGetValue(uid, out var last) && last == op.Objective.Id) continue;
                        tasks.Add(new UnitTask(uid, verb, op.Objective.Position, op.Objective.TargetId));
                        state.LastObjectiveByUnit[uid] = op.Objective.Id;
                    }
                }
            }
            // Forget units no longer tasked so they re-task cleanly if re-engaged.
            foreach (var k in state.LastObjectiveByUnit.Keys.Where(k => !tasked.Contains(k)).ToList())
                state.LastObjectiveByUnit.Remove(k);

            return tasks;
        }

        private static ThreatPicture ThreatNear(WorldSnapshot snapshot, Vec3 point, float radius)
        {
            var near = new List<EnemyView>();
            foreach (var e in snapshot.KnownEnemies)
                if (e != null && e.Position.HorizontalDistanceTo(point) <= radius) near.Add(e);
            return new ThreatPicture(near);
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

        /// <summary>
        /// Generate DestroyTarget objectives for known enemy clusters that no existing objective already
        /// covers. Highest strategic-priority enemies seed clusters first; deterministic.
        /// </summary>
        public static IReadOnlyList<Objective> GenerateObjectives(
            IReadOnlyList<EnemyView> known, IReadOnlyList<Objective> existing, BrainConfig cfg)
        {
            var result = new List<Objective>();
            var pool = (known ?? new List<EnemyView>())
                .Where(e => e != null)
                .OrderByDescending(e => e.StrategicPriority).ThenBy(e => e.Id) // deterministic
                .ToList();
            var used = new bool[pool.Count];
            int idx = 0;

            for (int i = 0; i < pool.Count; i++)
            {
                if (used[i]) continue;
                var seed = pool[i];
                float priority = seed.StrategicPriority;
                used[i] = true;
                for (int j = i + 1; j < pool.Count; j++)
                {
                    if (used[j]) continue;
                    if (pool[j].Position.HorizontalDistanceTo(seed.Position) <= cfg.ClusterRadius)
                    {
                        used[j] = true;
                        priority += pool[j].StrategicPriority;
                    }
                }

                bool covered = existing != null &&
                    existing.Any(o => o.Position.HorizontalDistanceTo(seed.Position) <= cfg.CoverageRadius);
                if (covered) continue;

                result.Add(new Objective($"auto-obj-{idx++}", ObjectiveKind.DestroyTarget, seed.Position,
                    ObjectiveSource.Auto, priority: priority));
            }
            return result;
        }

        /// <summary>
        /// Pick the squad ids best suited to an objective: matching family, not empty, not already assigned;
        /// strongest first, capped. (Proximity ordering is added once squad positions are threaded in.)
        /// </summary>
        public static IReadOnlyList<string> MatchSquads(Objective objective, IReadOnlyList<Squad> squads, BrainConfig cfg)
        {
            var suitable = Families.SuitableFor(objective.Kind);
            return (squads ?? new List<Squad>())
                .Where(s => s != null && !s.IsEmpty && s.AssignedOperationId == null && suitable.Contains(s.Family))
                .OrderByDescending(s => s.Strength).ThenBy(s => s.Id)
                .Take(cfg.MaxSquadsPerOperation)
                .Select(s => s.Id)
                .ToList();
        }
    }
}
