using System.Collections.Generic;
using System.Linq;
using CommanderLayer.Core.Command;
using CommanderLayer.Core.Model;
using Xunit;

namespace CommanderLayer.Tests
{
    public class TargetPrioritizerTests
    {
        private static Vec3 P(float x, float z) => new Vec3(x, 0, z);

        private static EnemyView E(string id, Vec3 pos, Role role = Role.Armor, float priority = 1f)
        {
            bool isAd = role == Role.GroundAirDefense || role == Role.AirDefenseShip;
            var cap = new UnitCapability(role, true, isAd, false, false, isAd);
            return new EnemyView(id, pos, UnitClass.GroundVehicle, cap, true, priority, 0);
        }

        private static ThreatGroup Group(params EnemyView[] members) => new ThreatGroup(members.ToList());

        private static readonly Doctrine Neutral = new Doctrine { RiskTolerance = 0.5f };

        [Fact]
        public void Higher_priority_group_outranks_lower_at_equal_distance()
        {
            var home = P(0, 0);
            var high = Group(E("h", P(10000, 0), Role.Armor, priority: 10f));
            var low = Group(E("l", P(0, 10000), Role.Armor, priority: 1f)); // same distance from home

            var ranked = TargetPrioritizer.Rank(new[] { low, high }, home, Neutral);

            Assert.Equal(2, ranked.Count);
            Assert.Same(high, ranked[0].Group);
            Assert.Same(low, ranked[1].Group);
        }

        [Fact]
        public void Closer_group_outranks_equal_priority_farther_one()
        {
            var home = P(0, 0);
            var near = Group(E("n", P(2000, 0), Role.Armor, priority: 3f));
            var far = Group(E("f", P(40000, 0), Role.Armor, priority: 3f));

            var ranked = TargetPrioritizer.Rank(new[] { far, near }, home, Neutral);

            Assert.Same(near, ranked[0].Group);
            Assert.Same(far, ranked[1].Group);
        }

        [Fact]
        public void AirDefense_group_gets_a_boost_over_an_equal_plain_group()
        {
            var home = P(0, 0);
            var airDefense = Group(E("ad", P(10000, 0), Role.GroundAirDefense, priority: 2f));
            var plain = Group(E("pl", P(0, 10000), Role.Armor, priority: 2f));

            var ranked = TargetPrioritizer.Rank(new[] { plain, airDefense }, home, Neutral);

            Assert.Same(airDefense, ranked[0].Group);
            Assert.True(ranked[0].Score > ranked[1].Score);
        }

        [Fact]
        public void Empty_input_yields_empty_ranking()
        {
            Assert.Empty(TargetPrioritizer.Rank(new List<ThreatGroup>(), P(0, 0), Neutral));
        }

        [Fact]
        public void Null_input_yields_empty_ranking()
        {
            Assert.Empty(TargetPrioritizer.Rank(null, P(0, 0), Neutral));
        }

        [Fact]
        public void Suggested_kind_is_capture_for_armor_destroy_for_air_defense()
        {
            var home = P(0, 0);
            var armor = Group(E("a", P(5000, 0), Role.Armor, priority: 1f));
            var sam = Group(E("s", P(6000, 0), Role.GroundAirDefense, priority: 1f));

            var ranked = TargetPrioritizer.Rank(new[] { armor, sam }, home, Neutral);

            var armorScored = ranked.Single(t => t.Group == armor);
            var samScored = ranked.Single(t => t.Group == sam);
            Assert.Equal(ObjectiveKind.CapturePoint, armorScored.SuggestedKind);
            Assert.Equal(ObjectiveKind.DestroyTarget, samScored.SuggestedKind);
        }

        [Fact]
        public void Aggressive_doctrine_values_a_high_priority_target_more_than_cautious()
        {
            var home = P(0, 0);
            var farHighValue = Group(E("fh", P(40000, 0), Role.Armor, priority: 10f));

            float aggressive = TargetPrioritizer.Rank(new[] { farHighValue }, home, new Doctrine { RiskTolerance = 1f })[0].Score;
            float cautious = TargetPrioritizer.Rank(new[] { farHighValue }, home, new Doctrine { RiskTolerance = 0f })[0].Score;

            Assert.True(aggressive > cautious); // aggression leans on strategic value
        }

        [Fact]
        public void Ordering_is_deterministic_for_tied_scores()
        {
            var home = P(0, 0);
            var a = Group(E("a", P(30000, 0), Role.Armor, priority: 1f));
            var b = Group(E("b", P(10000, 0), Role.Armor, priority: 1f));
            var c = Group(E("c", P(20000, 0), Role.Armor, priority: 1f));

            var first = TargetPrioritizer.Rank(new[] { a, b, c }, home, Neutral);
            var second = TargetPrioritizer.Rank(new[] { c, a, b }, home, Neutral);

            Assert.Equal(
                first.Select(t => t.Group.Center.X).ToList(),
                second.Select(t => t.Group.Center.X).ToList());
        }
    }
}
