using System.Collections.Generic;
using System.Linq;
using Nucleus.Core.Command;
using Nucleus.Core.Model;
using Xunit;

namespace Nucleus.Tests
{
    /// <summary>The pure decomposition: a goal + the threat at its place becomes a dependency-sequenced tree of
    /// prerequisite objectives sited on the threats they address, then the goal gated on all of them.</summary>
    public class OrderPlannerTests
    {
        private static EnemyView Air(string id, float x, float z)
            => new EnemyView(id, new Vec3(x, 0f, z), UnitClass.Aircraft,
                new UnitCapability(Role.Fighter, false, true, false, false, false), true, 2f, 1);

        private static EnemyView Sam(string id, float x, float z)
            => new EnemyView(id, new Vec3(x, 0f, z), UnitClass.GroundVehicle,
                new UnitCapability(Role.GroundAirDefense, false, true, false, false, isAirDefense: true), true, 3f, 2);

        private static EnemyView Ship(string id, float x, float z)
            => new EnemyView(id, new Vec3(x, 0f, z), UnitClass.Ship,
                new UnitCapability(Role.CombatShip, true, true, false, false, false), true, 3f, 3);

        private static EnemyView Radar(string id, float x, float z)
            => new EnemyView(id, new Vec3(x, 0f, z), UnitClass.GroundVehicle,
                new UnitCapability(Role.GroundRadar, false, false, false, false, false), true, 2f, 1);

        [Fact]
        public void Undefended_goal_decomposes_to_exactly_itself()
        {
            var plan = OrderPlanner.Decompose(ObjectiveKind.CapturePoint, new Vec3(1000, 0, 0), 5f,
                ObjectiveSource.Auto, ThreatPicture.Empty);

            var child = Assert.Single(plan.Children);
            Assert.Equal(ObjectiveKind.CapturePoint, child.Kind);
            Assert.Empty(child.DependsOnIndices);     // nothing to wait on — baseline-identical
        }

        [Fact]
        public void Layered_threat_yields_ordered_prerequisites_and_a_gated_goal()
        {
            var threat = new ThreatPicture(new List<EnemyView>
            {
                Radar("r1", 4000, 0),
                Air("a1", 3000, 2000),
                Sam("s1", 4100, 0), Sam("s2", 4100, 100), // belt of 2 > tolerance(1) -> dedicated SEAD
                Ship("h1", 5000, -1000),
            });
            var plan = OrderPlanner.Decompose(ObjectiveKind.CapturePoint, new Vec3(4000, 0, 0), 9f,
                ObjectiveSource.Auto, threat, new Doctrine());

            var kinds = plan.Children.Select(c => c.Kind).ToList();
            Assert.Equal(new[]
            {
                ObjectiveKind.Recon, ObjectiveKind.ControlAirspace, ObjectiveKind.SuppressAirDefense,
                ObjectiveKind.NavalStrike, ObjectiveKind.CapturePoint,
            }, kinds);

            // The goal (last child) depends on every prerequisite.
            var goal = plan.Children[plan.GoalIndex];
            Assert.Equal(ObjectiveKind.CapturePoint, goal.Kind);
            Assert.Equal(new[] { 0, 1, 2, 3 }, goal.DependsOnIndices.ToArray());
            // Prerequisites are unconstrained.
            for (int i = 0; i < plan.GoalIndex; i++) Assert.Empty(plan.Children[i].DependsOnIndices);
        }

        [Fact]
        public void A_single_tolerated_SAM_gets_no_dedicated_SEAD()
        {
            var threat = new ThreatPicture(new List<EnemyView> { Sam("s1", 4100, 0) });
            var plan = OrderPlanner.Decompose(ObjectiveKind.DestroyTarget, new Vec3(4100, 0, 0), 3f,
                ObjectiveSource.Auto, threat, new Doctrine());   // MaxResidualAirDefense == 1

            Assert.DoesNotContain(plan.Children, c => c.Kind == ObjectiveKind.SuppressAirDefense);
            Assert.Single(plan.Children);   // just the goal
        }

        [Fact]
        public void A_first_class_naval_goal_does_not_spawn_a_naval_prerequisite()
        {
            var threat = new ThreatPicture(new List<EnemyView> { Ship("h1", 5000, 0), Ship("h2", 5100, 0) });
            var plan = OrderPlanner.Decompose(ObjectiveKind.NavalStrike, new Vec3(5000, 0, 0), 4f,
                ObjectiveSource.Auto, threat, new Doctrine());

            var child = Assert.Single(plan.Children);
            Assert.Equal(ObjectiveKind.NavalStrike, child.Kind);
        }

        [Fact]
        public void Prerequisites_are_sited_on_the_threat_they_address()
        {
            var threat = new ThreatPicture(new List<EnemyView>
            {
                Air("a1", 3000, 2000),
                Ship("h1", 6000, -4000),
            });
            var plan = OrderPlanner.Decompose(ObjectiveKind.CapturePoint, new Vec3(4000, 0, 0), 5f,
                ObjectiveSource.Auto, threat, new Doctrine());

            var air = plan.Children.First(c => c.Kind == ObjectiveKind.ControlAirspace);
            var naval = plan.Children.First(c => c.Kind == ObjectiveKind.NavalStrike);
            Assert.Equal(3000f, air.Position.X, 1);   // on the aircraft, not the goal point
            Assert.Equal(6000f, naval.Position.X, 1); // on the ship
        }
    }
}
