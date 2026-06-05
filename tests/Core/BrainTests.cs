using System.Collections.Generic;
using System.Linq;
using CommanderLayer.Core.Command;
using CommanderLayer.Core.Model;
using Xunit;

namespace CommanderLayer.Tests
{
    public class BrainTests
    {
        private static Vec3 P(float x, float z) => new Vec3(x, 0, z);

        private static EnemyView E(string id, Vec3 pos, float priority = 1f) =>
            new EnemyView(id, pos, UnitClass.GroundVehicle, default, true, priority, 0);

        private static Squad Sq(string id, RoleFamily fam, int strength, string assignedOp = null)
        {
            var s = new Squad(id, id, fam, SquadOrigin.Auto, Enumerable.Range(0, strength).Select(i => id + "u" + i));
            s.AssignedOperationId = assignedOp;
            return s;
        }

        private static BrainConfig Cfg() => new BrainConfig { ClusterRadius = 3000f, CoverageRadius = 4000f, MaxSquadsPerOperation = 2 };
        private static SquadConfig SquadCfg() => new SquadConfig { FormRadius = 4000f, MaxSquadSize = 5 };

        private static UnitView U(string id, Role role, Vec3 pos) =>
            new UnitView(id, id, pos, UnitClass.GroundVehicle, false, true,
                new UnitCapability(role, true, false, false, false, false), 1f, 0f, 1);

        [Fact]
        public void Tick_generates_operation_and_tasks_from_threat()
        {
            var state = new CommanderState(SquadCfg(), null, Cfg());
            var roster = new List<UnitView> { U("a1", Role.Armor, P(0, 0)), U("a2", Role.Armor, P(100, 0)) };
            var known = new List<EnemyView> { E("e1", P(5000, 0)) };

            var tasks = CommanderBrain.Tick(new WorldSnapshot(roster, known), state);

            Assert.Single(state.Objectives);
            Assert.Single(state.Operations);
            Assert.Equal(OperationStatus.Active, state.Operations[0].Status);
            Assert.Equal(2, tasks.Count);                                  // both armor units tasked
            Assert.All(tasks, t => Assert.Equal(TaskVerb.MoveTo, t.Verb));
        }

        [Fact]
        public void Tick_with_no_enemies_does_nothing()
        {
            var state = new CommanderState(SquadCfg(), null, Cfg());
            var tasks = CommanderBrain.Tick(new WorldSnapshot(new List<UnitView>(), new List<EnemyView>()), state);
            Assert.Empty(state.Objectives);
            Assert.Empty(state.Operations);
            Assert.Empty(tasks);
        }

        [Fact]
        public void Tick_does_nothing_when_commander_is_manual()
        {
            var state = new CommanderState(SquadCfg(), null, Cfg()) { Autonomy = AutonomyLevel.Manual };
            var roster = new List<UnitView> { U("a1", Role.Armor, P(0, 0)) };
            var known = new List<EnemyView> { E("e1", P(5000, 0)) };
            var tasks = CommanderBrain.Tick(new WorldSnapshot(roster, known), state);
            Assert.Empty(tasks);
            Assert.Empty(state.Operations);
        }

        [Fact]
        public void GenerateObjectives_clusters_nearby_enemies_into_one()
        {
            var known = new List<EnemyView> { E("e1", P(0, 0)), E("e2", P(500, 0)), E("e3", P(50000, 0)) };
            var objs = CommanderBrain.GenerateObjectives(known, new List<Objective>(), Cfg());
            Assert.Equal(2, objs.Count);                              // {e1,e2} cluster + e3
            Assert.All(objs, o => Assert.Equal(ObjectiveKind.DestroyTarget, o.Kind));
        }

        [Fact]
        public void GenerateObjectives_skips_areas_already_covered()
        {
            var known = new List<EnemyView> { E("e1", P(0, 0)) };
            var existing = new List<Objective> { new Objective("x", ObjectiveKind.DestroyTarget, P(1000, 0), ObjectiveSource.Player) };
            Assert.Empty(CommanderBrain.GenerateObjectives(known, existing, Cfg())); // within CoverageRadius
        }

        [Fact]
        public void MatchSquads_picks_suitable_unassigned_strongest_first_capped()
        {
            var obj = new Objective("o", ObjectiveKind.DestroyTarget, P(0, 0), ObjectiveSource.Auto);
            var squads = new List<Squad>
            {
                Sq("armorBig", RoleFamily.Armor, 4),
                Sq("armorSmall", RoleFamily.Armor, 1),
                Sq("art", RoleFamily.Artillery, 2),
                Sq("supply", RoleFamily.Supply, 3),          // wrong family -> excluded
                Sq("busy", RoleFamily.Armor, 5, assignedOp: "other"), // already assigned -> excluded
            };
            var chosen = CommanderBrain.MatchSquads(obj, squads, Cfg());
            Assert.Equal(new[] { "armorBig", "art" }, chosen.ToArray()); // strongest suitable, capped at 2
        }
    }
}
