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

            state.Squads.Reconcile(snapshot.Roster);

            // Drop operations whose squads are gone (disbanded/dead) so their objectives can be re-planned.
            foreach (var op in state.Operations)
            {
                if (op.IsTerminal) continue;
                op.SquadIds.RemoveAll(sid => state.Squads.ById(sid) == null);
                if (op.SquadIds.Count == 0) op.Status = OperationStatus.Failed;
            }

            // New objectives from known enemy clusters not already covered.
            foreach (var obj in GenerateObjectives(snapshot.KnownEnemies, state.Objectives, state.BrainConfig))
                state.Objectives.Add(obj);

            // Open an operation for each uncovered objective, matching a suitable free force.
            foreach (var obj in state.Objectives)
            {
                if (state.OperationFor(obj.Id) != null) continue;
                var squadIds = MatchSquads(obj, state.Squads.Squads, state.BrainConfig);
                if (squadIds.Count == 0) continue; // no force available — production request comes in P3
                var op = new Operation(state.NextOperationId(), obj, squadIds) { Status = OperationStatus.Active };
                foreach (var sid in squadIds) state.Squads.ById(sid).AssignedOperationId = op.Id;
                state.Operations.Add(op);
            }

            // Issue tasking: each active operation moves/attacks with its assigned squads' units.
            foreach (var op in state.Operations)
            {
                if (op.Status != OperationStatus.Active) continue;
                var verb = op.Objective.TargetId != null && op.Objective.Kind == ObjectiveKind.DestroyTarget
                    ? TaskVerb.AttackTarget : TaskVerb.MoveTo;
                foreach (var sid in op.SquadIds)
                {
                    var squad = state.Squads.ById(sid);
                    if (squad == null) continue;
                    foreach (var uid in squad.MemberUnitIds)
                        tasks.Add(new UnitTask(uid, verb, op.Objective.Position, op.Objective.TargetId));
                }
            }
            return tasks;
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
