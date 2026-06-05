using System.Collections.Generic;
using System.Linq;
using CommanderLayer.Core.Command;
using CommanderLayer.Core.Model;
using Xunit;

namespace CommanderLayer.Tests
{
    public class ThreatBoardTests
    {
        private static Vec3 P(float x, float z) => new Vec3(x, 0, z);

        // Generic ground contact with a chosen role + priority.
        private static EnemyView E(string id, Vec3 pos, Role role = Role.Armor, float priority = 1f)
        {
            bool isAd = role == Role.GroundAirDefense || role == Role.AirDefenseShip;
            var cap = new UnitCapability(role, true, isAd, false, false, isAd);
            return new EnemyView(id, pos, UnitClass.GroundVehicle, cap, true, priority, 0);
        }

        [Fact]
        public void Nearby_enemies_cluster_while_far_one_is_separate()
        {
            var known = new List<EnemyView>
            {
                E("e1", P(0, 0)),
                E("e2", P(500, 0)),
                E("e3", P(50000, 0)),
            };

            var board = ThreatBoard.Build(known, clusterRadius: 3000f);

            Assert.Equal(2, board.Count);
            var big = board.OrderByDescending(g => g.Count).First();
            Assert.Equal(2, big.Count);                 // {e1, e2}
            var lone = board.Single(g => g.Count == 1); // e3 on its own
            Assert.Equal(1, lone.Count);
        }

        [Fact]
        public void TotalStrategicPriority_sums_members()
        {
            var known = new List<EnemyView>
            {
                E("e1", P(0, 0), priority: 2f),
                E("e2", P(400, 0), priority: 3f),
            };

            var board = ThreatBoard.Build(known, clusterRadius: 3000f);

            var group = Assert.Single(board);
            Assert.Equal(2, group.Count);
            Assert.Equal(5f, group.TotalStrategicPriority, 3);
        }

        [Fact]
        public void Dominant_family_is_the_majority()
        {
            var known = new List<EnemyView>
            {
                E("a1", P(0, 0), Role.Armor),
                E("a2", P(100, 0), Role.Armor),
                E("art1", P(200, 0), Role.Artillery),
            };

            var board = ThreatBoard.Build(known, clusterRadius: 3000f);

            var group = Assert.Single(board);
            Assert.Equal(RoleFamily.Armor, group.Dominant);
        }

        [Fact]
        public void AirDefense_and_armor_cluster_reports_threat_with_counts()
        {
            var known = new List<EnemyView>
            {
                E("sam1", P(0, 0), Role.GroundAirDefense),
                E("sam2", P(150, 0), Role.GroundAirDefense),
                E("mbt1", P(300, 0), Role.Armor),
            };

            var board = ThreatBoard.Build(known, clusterRadius: 3000f);

            var group = Assert.Single(board);
            Assert.True(group.Threat.HasAirDefense);
            Assert.Equal(2, group.Threat.AirDefenseCount);
            Assert.True(group.Threat.HasArmor);
            Assert.Equal(1, group.Threat.ArmorCount);
            Assert.Equal(3, group.Threat.Count);
        }

        [Fact]
        public void Empty_input_yields_empty_board()
        {
            Assert.Empty(ThreatBoard.Build(new List<EnemyView>(), clusterRadius: 3000f));
        }

        [Fact]
        public void Null_entries_are_skipped()
        {
            var known = new List<EnemyView> { E("e1", P(0, 0)), null, E("e2", P(400, 0)) };

            var board = ThreatBoard.Build(known, clusterRadius: 3000f);

            var group = Assert.Single(board);
            Assert.Equal(2, group.Count);
        }

        [Fact]
        public void Clustering_is_independent_of_input_order()
        {
            var a = E("a", P(0, 0));
            var b = E("b", P(500, 0));
            var c = E("c", P(50000, 0));
            var board1 = ThreatBoard.Build(new List<EnemyView> { a, b, c }, 3000f);
            var board2 = ThreatBoard.Build(new List<EnemyView> { c, b, a }, 3000f);

            Assert.Equal(
                board1.Select(g => g.Count).OrderBy(x => x),
                board2.Select(g => g.Count).OrderBy(x => x));
        }

        [Fact]
        public void ThreatGroup_throws_on_empty_or_null_members()
        {
            Assert.Throws<System.ArgumentException>(() => new ThreatGroup(new List<EnemyView>()));
            Assert.Throws<System.ArgumentException>(() => new ThreatGroup(new List<EnemyView> { null }));
        }
    }
}
