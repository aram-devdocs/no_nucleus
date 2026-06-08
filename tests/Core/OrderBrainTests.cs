using System.Collections.Generic;
using System.Linq;
using Nucleus.Core.Command;
using Nucleus.Core.Model;
using Nucleus.Core.Persistence;
using Xunit;

namespace Nucleus.Tests
{
    /// <summary>The brain side of the order layer: decomposition on generation + player drop, dependency gating
    /// (a goal gets no force until its prerequisites resolve), order completion/fade, and save/resume of the tree.</summary>
    public class OrderBrainTests
    {
        private static EnemyView Armor(string id, float x, float z)
            => new EnemyView(id, new Vec3(x, 0f, z), UnitClass.GroundVehicle,
                new UnitCapability(Role.Armor, true, false, true, false, false), true, 2f, 3);

        private static EnemyView Air(string id, float x, float z)
            => new EnemyView(id, new Vec3(x, 0f, z), UnitClass.Aircraft,
                new UnitCapability(Role.Fighter, false, true, false, false, false), true, 3f, 1);

        private static UnitView Unit(string id, Role role, UnitClass cls)
            => new UnitView(id, id, new Vec3(0, 0, 0), cls, false, true,
                new UnitCapability(role, role == Role.Armor, role == Role.Fighter, false, false, false),
                role == Role.Armor ? 1f : 0.3f, role == Role.Fighter ? 1f : 0f, role == Role.Armor ? 3 : 1);

        // An armor pocket (→ CapturePoint goal) screened by one aircraft (→ ControlAirspace prerequisite).
        private static List<EnemyView> DefendedCluster() => new List<EnemyView>
        {
            Armor("e1", 4000, 0), Armor("e2", 4100, 0), Armor("e3", 4000, 100),
            Air("e-air", 4000, 300),
        };

        [Fact]
        public void A_defended_cluster_generates_an_order_tree_with_a_gated_goal()
        {
            var state = new CommanderState();
            CommanderBrain.Tick(new WorldSnapshot(new List<UnitView>(), DefendedCluster(), 0f, null, 1f), state);

            var order = Assert.Single(state.Orders);
            var kinds = order.ChildObjectiveIds
                .Select(id => state.Objectives.First(o => o.Id == id).Kind).ToList();
            Assert.Contains(ObjectiveKind.ControlAirspace, kinds);
            Assert.Contains(ObjectiveKind.CapturePoint, kinds);

            var goal = state.Objectives.First(o => o.Id == order.GoalObjectiveId);
            Assert.NotEmpty(goal.DependsOn);   // the goal waits on the air prerequisite
        }

        [Fact]
        public void The_goal_gets_no_force_until_the_prerequisite_resolves()
        {
            var state = new CommanderState();
            state.Squads.Add(new Squad("sq-air", "Air", RoleFamily.AirCombat, SquadOrigin.Auto, new[] { "u-air" }));
            state.Squads.Add(new Squad("sq-arm", "Armor", RoleFamily.Armor, SquadOrigin.Auto, new[] { "u-arm" }));
            var roster = new List<UnitView> { Unit("u-air", Role.Fighter, UnitClass.Aircraft), Unit("u-arm", Role.Armor, UnitClass.GroundVehicle) };

            // Tick 1: prerequisite (aircraft) still alive -> the goal is gated.
            CommanderBrain.Tick(new WorldSnapshot(roster, DefendedCluster(), 0f, null, 1f), state);
            var order = Assert.Single(state.Orders);
            var goalId = order.GoalObjectiveId;
            Assert.Null(state.OperationFor(goalId));                       // goal NOT fielded
            var airId = order.ChildObjectiveIds.First(id => state.Objectives.First(o => o.Id == id).Kind == ObjectiveKind.ControlAirspace);
            Assert.NotNull(state.OperationFor(airId));                     // prerequisite IS fielded

            // Tick 2+: the aircraft is gone -> ControlAirspace resolves -> the goal ungates and fields armor.
            var clear = new List<EnemyView> { Armor("e1", 4000, 0), Armor("e2", 4100, 0), Armor("e3", 4000, 100) };
            for (int t = 0; t < 3; t++)
                CommanderBrain.Tick(new WorldSnapshot(roster, clear, 0f, null, 2f + t), state);

            Assert.NotNull(state.OperationFor(goalId));                    // goal now fielded
        }

        [Fact]
        public void A_player_drop_decomposes_into_an_order_and_returns_the_goal()
        {
            var state = new CommanderState();
            var snap = new WorldSnapshot(new List<UnitView>(), DefendedCluster(), 0f, null, 1f);

            var goalId = CommanderBrain.CreatePlayerObjective(state, snap, ObjectiveKind.CapturePoint, new Vec3(4000, 0, 0));

            var order = Assert.Single(state.Orders);
            Assert.Equal(ObjectiveSource.Player, order.Source);
            Assert.Equal(goalId, order.GoalObjectiveId);
            var goal = state.Objectives.First(o => o.Id == goalId);
            Assert.Equal(ObjectiveSource.Player, goal.Source);
            Assert.NotEmpty(goal.DependsOn);
        }

        [Fact]
        public void An_order_completes_when_its_goal_is_achieved_then_fades_away()
        {
            var state = new CommanderState();
            CommanderBrain.Tick(new WorldSnapshot(new List<UnitView>(), DefendedCluster(), 0f, null, 1f), state);
            var order = Assert.Single(state.Orders);

            // Freeze generation and clear the goal objective to simulate it being achieved.
            state.AiCreatesObjectives = false;
            state.Objectives.RemoveAll(o => o.Id == order.GoalObjectiveId);

            CommanderBrain.Tick(new WorldSnapshot(new List<UnitView>(), new List<EnemyView>(), 0f, null, 2f), state);
            Assert.Equal(OrderStatus.Complete, state.Orders.Single().Status);

            // After the fade grace window it is pruned.
            CommanderBrain.Tick(new WorldSnapshot(new List<UnitView>(), new List<EnemyView>(), 0f, null, 100f), state);
            Assert.Empty(state.Orders);
        }

        [Fact]
        public void Orders_and_dependencies_survive_save_and_resume()
        {
            var state = new CommanderState();
            CommanderBrain.Tick(new WorldSnapshot(new List<UnitView>(), DefendedCluster(), 0f, null, 1f), state);
            Assert.NotEmpty(state.Orders);

            var restored = CampaignState.Restore(CampaignSave.Deserialize(CampaignSave.Serialize(CampaignState.Capture(state))));

            Assert.Equal(state.Orders.Count, restored.Orders.Count);
            var o = state.Orders[0];
            var r = restored.Orders.First(x => x.Id == o.Id);
            Assert.Equal(o.GoalKind, r.GoalKind);
            Assert.Equal(o.GoalObjectiveId, r.GoalObjectiveId);
            Assert.Equal(o.ChildObjectiveIds, r.ChildObjectiveIds);

            var goalOrig = state.Objectives.First(x => x.Id == o.GoalObjectiveId);
            var goalRest = restored.Objectives.First(x => x.Id == r.GoalObjectiveId);
            Assert.Equal(goalOrig.OrderId, goalRest.OrderId);
            Assert.Equal(goalOrig.DependsOn, goalRest.DependsOn);
        }
    }
}
