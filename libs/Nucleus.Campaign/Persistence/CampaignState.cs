using System.Collections.Generic;
using Nucleus.Core.Command;
using Nucleus.Core.Model;

namespace Nucleus.Core.Persistence
{
    /// <summary>
    /// Maps a live <see cref="CommanderState"/> to/from a <see cref="CampaignSnapshot"/> — the save/resume
    /// seam for a long-lived campaign. Pure and deterministic. Restore reconstructs fresh domain instances
    /// (objectives, squads, operations) so the result is independent of the snapshot, and reattaches each
    /// operation to its objective by id. Counters (operation-id, squad batch) are preserved so newly issued
    /// ids never collide with restored ones.
    /// </summary>
    public static class CampaignState
    {
        public static CampaignSnapshot Capture(CommanderState state)
        {
            var snap = new CampaignSnapshot
            {
                AiCreatesObjectives = state.AiCreatesObjectives,
                AiAutoFill = state.AiAutoFill,
                HomeBase = state.HomeBase,
                OperationIdSeed = state.OperationIdSeed,
                ObjectiveIdSeed = state.ObjectiveIdSeed,
                OrderIdSeed = state.OrderIdSeed,
                SquadBatchSeed = state.Squads.BatchSeed,
                RiskTolerance = state.Doctrine.RiskTolerance,
                ForceRatio = state.Doctrine.ForceRatio,
                ClusterRadius = state.BrainConfig.ClusterRadius,
                CoverageRadius = state.BrainConfig.CoverageRadius,
                MaxSquadsPerOperation = state.BrainConfig.MaxSquadsPerOperation,
                FormRadius = state.Squads.Config.FormRadius,
                MaxSquadSize = state.Squads.Config.MaxSquadSize,
                DepletedFraction = state.Squads.Config.DepletedFraction,
            };

            var objIds = new HashSet<string>();
            foreach (var o in state.Objectives) { snap.Objectives.Add(o); objIds.Add(o.Id); }
            // Fold in any operation objective not in the list so the snapshot is self-contained.
            foreach (var op in state.Operations)
                if (op.Objective != null && objIds.Add(op.Objective.Id)) snap.Objectives.Add(op.Objective);

            foreach (var ord in state.Orders) snap.Orders.Add(ord);
            foreach (var s in state.Squads.Squads) snap.Squads.Add(s);
            foreach (var op in state.Operations) snap.Operations.Add(op);
            foreach (var id in state.ConfirmedObjectives) snap.ConfirmedObjectives.Add(id);
            foreach (var kv in state.LastObjectiveByUnit)
                snap.LastObjectiveByUnit.Add(new KeyValuePair<string, string>(kv.Key, kv.Value));

            return snap;
        }

        public static CommanderState Restore(CampaignSnapshot snap)
        {
            var squadCfg = new SquadConfig
            {
                FormRadius = snap.FormRadius,
                MaxSquadSize = snap.MaxSquadSize,
                DepletedFraction = snap.DepletedFraction,
            };
            var doctrine = new Doctrine { RiskTolerance = snap.RiskTolerance, ForceRatio = snap.ForceRatio };
            var brainCfg = new BrainConfig
            {
                ClusterRadius = snap.ClusterRadius,
                CoverageRadius = snap.CoverageRadius,
                MaxSquadsPerOperation = snap.MaxSquadsPerOperation,
            };

            var state = new CommanderState(squadCfg, doctrine, brainCfg)
            {
                AiCreatesObjectives = snap.AiCreatesObjectives,
                AiAutoFill = snap.AiAutoFill,
                HomeBase = snap.HomeBase,
                OperationIdSeed = snap.OperationIdSeed,
                ObjectiveIdSeed = snap.ObjectiveIdSeed,
                OrderIdSeed = snap.OrderIdSeed,
            };
            state.Squads.BatchSeed = snap.SquadBatchSeed;

            // Objectives — rebuilt fresh, indexed so operations can reattach by id.
            var byId = new Dictionary<string, Objective>();
            foreach (var o in snap.Objectives)
            {
                var deps = o.DependsOn != null && o.DependsOn.Count > 0
                    ? new List<string>(o.DependsOn) : (IReadOnlyList<string>)null;
                var copy = new Objective(o.Id, o.Kind, o.Position, o.Source, o.TargetId, o.Priority, o.OrderId, deps);
                state.Objectives.Add(copy);
                byId[copy.Id] = copy;
            }

            // Orders — rebuilt fresh from the snapshot (children/goal reference objective ids, no object graph).
            foreach (var o in snap.Orders)
            {
                var copy = new Order(o.Id, o.GoalKind, o.Position, o.Priority, o.Source)
                {
                    Autonomy = o.Autonomy,
                    Status = o.Status,
                    GoalObjectiveId = o.GoalObjectiveId,
                    TerminalTime = o.TerminalTime,
                    CreatedTime = o.CreatedTime,
                };
                copy.ChildObjectiveIds.AddRange(o.ChildObjectiveIds);
                state.Orders.Add(copy);
            }

            foreach (var s in snap.Squads)
            {
                var copy = new Squad(s.Id, s.Name, s.Family, s.Origin, s.MemberUnitIds)
                {
                    AssignedOperationId = s.AssignedOperationId,
                    Status = s.Status,
                    Autonomy = s.Autonomy,
                    TargetComposition = CloneComposition(s.TargetComposition),
                };
                state.Squads.Add(copy);
            }

            foreach (var op in snap.Operations)
            {
                if (op.Objective == null || !byId.TryGetValue(op.Objective.Id, out var obj)) continue;
                var copy = new Operation(op.Id, obj, op.SquadIds)
                {
                    Autonomy = op.Autonomy,
                    Status = op.Status,
                    CombatPhase = op.CombatPhase,
                    InitialThreat = CloneThreat(op.InitialThreat),
                };
                state.Operations.Add(copy);
            }

            foreach (var id in snap.ConfirmedObjectives) state.ConfirmedObjectives.Add(id);
            foreach (var kv in snap.LastObjectiveByUnit) state.LastObjectiveByUnit[kv.Key] = kv.Value;

            return state;
        }

        private static Composition CloneComposition(Composition c)
        {
            if (c == null) return null;
            var copy = new Composition();
            foreach (var kv in c.Items) copy.Set(kv.Key, kv.Value);
            return copy;
        }

        private static ThreatPicture CloneThreat(ThreatPicture t)
        {
            if (t == null) return null;
            var enemies = new List<EnemyView>(t.Enemies);
            return new ThreatPicture(enemies);
        }
    }
}
