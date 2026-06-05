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
                    state.Log.Append(new ReportEvent(snapshot.Time, ReportKind.PhaseChanged, $"-> {op.CombatPhase}", op.Id));
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
            // Drop stale confirmations whose objective is gone (no unbounded growth).
            state.ConfirmedObjectives.RemoveWhere(id => !state.Objectives.Any(o => o.Id == id));

            // 3. New objectives from known enemy clusters not already covered.
            foreach (var obj in GenerateObjectives(snapshot.KnownEnemies, state.Objectives, state.BrainConfig,
                         state.HomeBase, state.Doctrine))
                state.Objectives.Add(obj);

            // 4. Open an operation for each uncovered objective, matching a suitable free force. Squad
            //    positions (member centroids) let MatchSquads send the NEAREST suitable squad, not a
            //    cross-map one — combined arms that engages locally instead of streaming across the theater.
            //    Under ASSISTED autonomy the brain does not open operations on its own — it surfaces a
            //    Proposal per fieldable objective and waits for the player to confirm (autonomy ladder).
            bool assisted = state.Autonomy == AutonomyLevel.Assisted;
            state.Proposals.Clear();
            var fieldable = new HashSet<string>(); // objectives we have a force for (opened OR proposed)
            var positions = new Dictionary<string, Vec3>();
            foreach (var u in snapshot.Roster) if (u != null) positions[u.Id] = u.Position;
            foreach (var obj in state.Objectives)
            {
                if (state.OperationFor(obj.Id) != null) continue;
                var squadIds = MatchSquads(obj, state.Squads.Squads, state.BrainConfig, positions);
                if (squadIds.Count == 0) continue; // no force available — production request comes in P3
                fieldable.Add(obj.Id);
                if (assisted && !state.ConfirmedObjectives.Contains(obj.Id))
                {
                    // Propose, don't commit. RefId = objective id so ConfirmProposal can authorise it.
                    state.Proposals.Add(new Proposal(ProposalKind.OpenOperation,
                        $"{obj.Kind} ({squadIds.Count} squad{(squadIds.Count == 1 ? "" : "s")})", obj.Id, obj.Priority));
                    continue;
                }
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
                // Consume any Assisted confirmation — it authorises ONE open. If this op later fails with the
                // threat still up, the brain re-proposes rather than silently re-launching (review SHOULD-FIX).
                state.ConfirmedObjectives.Remove(obj.Id);
                state.Log.Append(new ReportEvent(snapshot.Time, ReportKind.OperationStarted,
                    $"{obj.Kind} ({squadIds.Count} squad{(squadIds.Count == 1 ? "" : "s")})", op.Id));
            }

            // 4b. Production needs: any objective we couldn't field a force for becomes a force request the
            //     Game layer turns into convoy buys. Recomputed each tick.
            state.ProductionNeeds.Clear();
            foreach (var obj in state.Objectives)
                if (state.OperationFor(obj.Id) == null && !fieldable.Contains(obj.Id)) // not just awaiting a confirm
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
                foreach (var sid in op.SquadIds)
                {
                    var squad = state.Squads.ById(sid);
                    if (squad == null || !active.Contains(squad.Family)) continue; // not this phase's turn — hold back
                    if (squad.Autonomy == AutonomyLevel.Manual) continue;          // player drives this squad directly
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

                result.Add(new Objective($"auto-obj-{idx++}", ObjectiveKind.DestroyTarget, center,
                    ObjectiveSource.Auto, priority: st.Score));
            }
            return result;
        }

        /// <summary>
        /// Pick the squad ids best suited to an objective: matching family, not empty, not already assigned,
        /// not player-owned (Manual). When <paramref name="positions"/> (unit id -> world position) is given,
        /// order by the NEAREST squad first (centroid of its members), then strongest, then id — so a local
        /// threat draws the local force, not a stronger one across the map. With no positions, falls back to
        /// strongest-first (unchanged). Capped at <see cref="BrainConfig.MaxSquadsPerOperation"/>.
        /// </summary>
        public static IReadOnlyList<string> MatchSquads(Objective objective, IReadOnlyList<Squad> squads, BrainConfig cfg,
            IReadOnlyDictionary<string, Vec3> positions = null)
        {
            var suitable = Families.SuitableFor(objective.Kind);
            var candidates = (squads ?? new List<Squad>())
                .Where(s => s != null && !s.IsEmpty && s.AssignedOperationId == null
                    && s.Autonomy != AutonomyLevel.Manual // player-owned squad — never auto-pulled into an op
                    && suitable.Contains(s.Family));
            var ranked = positions == null
                ? candidates.OrderByDescending(s => s.Strength).ThenBy(s => s.Id)
                : candidates.OrderBy(s => SquadDistance(s, objective.Position, positions))
                            .ThenByDescending(s => s.Strength).ThenBy(s => s.Id);
            return ranked.Take(cfg.MaxSquadsPerOperation).Select(s => s.Id).ToList();
        }

        // Horizontal distance from a squad's member centroid to a point. Unpositioned squads sort last.
        private static float SquadDistance(Squad s, Vec3 target, IReadOnlyDictionary<string, Vec3> positions)
        {
            float sumX = 0f, sumZ = 0f; int n = 0;
            foreach (var id in s.MemberUnitIds)
                if (positions.TryGetValue(id, out var p)) { sumX += p.X; sumZ += p.Z; n++; }
            if (n == 0) return float.MaxValue;
            return new Vec3(sumX / n, 0f, sumZ / n).HorizontalDistanceTo(target);
        }
    }
}
