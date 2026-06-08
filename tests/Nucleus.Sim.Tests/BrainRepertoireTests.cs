using System.Collections.Generic;
using System.Linq;
using Nucleus.Core.Command;
using Nucleus.Core.Model;
using Xunit;

namespace Nucleus.Sim.Tests
{
    /// <summary>The AI commander must field a believable MIX of objective kinds, not only DestroyTarget.
    /// Asserts the repertoire directly on the pure brain.</summary>
    public class BrainRepertoireTests
    {
        private static EnemyView Enemy(string id, float x, float z, Role role, bool isAirDefense, UnitClass cls, float prio)
            => new EnemyView(id, new Vec3(x, 0f, z), cls,
                new UnitCapability(role, canEngageGround: role == Role.Armor, canEngageAir: isAirDefense,
                    canCapture: false, isSupply: false, isAirDefense: isAirDefense),
                accurate: true, strategicPriority: prio, armorTier: 3);

        [Fact]
        public void Armor_pocket_becomes_a_CapturePoint_not_a_DestroyTarget()
        {
            var known = new List<EnemyView>
            {
                Enemy("a1", 4000f, 0f, Role.Armor, false, UnitClass.GroundVehicle, 2f),
                Enemy("a2", 4100f, 0f, Role.Armor, false, UnitClass.GroundVehicle, 2f),
                Enemy("a3", 4000f, 100f, Role.Armor, false, UnitClass.GroundVehicle, 2f),
            };
            var objs = CommanderBrain.GenerateObjectives(known, new List<Objective>(), new BrainConfig(),
                new Vec3(0, 0, 0), new Doctrine());
            Assert.Contains(objs, o => o.Kind == ObjectiveKind.CapturePoint);
        }

        [Fact]
        public void Air_defense_pocket_stays_a_DestroyTarget()
        {
            var known = new List<EnemyView>
            {
                Enemy("s1", 4000f, 0f, Role.GroundAirDefense, true, UnitClass.GroundVehicle, 3f),
                Enemy("s2", 4100f, 0f, Role.GroundAirDefense, true, UnitClass.GroundVehicle, 3f),
            };
            var objs = CommanderBrain.GenerateObjectives(known, new List<Objective>(), new BrainConfig(),
                new Vec3(0, 0, 0), new Doctrine());
            Assert.Contains(objs, o => o.Kind == ObjectiveKind.DestroyTarget);
            Assert.DoesNotContain(objs, o => o.Kind == ObjectiveKind.CapturePoint);
        }

        [Fact]
        public void Aircraft_pocket_becomes_a_ControlAirspace_objective()
        {
            var known = new List<EnemyView>
            {
                Enemy("f1", 4000f, 0f, Role.Fighter, false, UnitClass.Aircraft, 3f),
                Enemy("f2", 4100f, 0f, Role.Fighter, false, UnitClass.Aircraft, 3f),
            };
            var objs = CommanderBrain.GenerateObjectives(known, new List<Objective>(), new BrainConfig(),
                new Vec3(0, 0, 0), new Doctrine());
            Assert.Contains(objs, o => o.Kind == ObjectiveKind.ControlAirspace);
        }

        [Fact]
        public void Unidentified_pocket_becomes_a_Recon_objective()
        {
            // Every contact in the pocket is low-confidence (accurate:false) -> scout it before committing.
            var fuzzy = new List<EnemyView>
            {
                new EnemyView("u1", new Vec3(4000f, 0f, 0f), UnitClass.GroundVehicle,
                    new UnitCapability(Role.Armor, true, false, false, false, false), accurate: false, strategicPriority: 2f, armorTier: 3),
                new EnemyView("u2", new Vec3(4100f, 0f, 0f), UnitClass.GroundVehicle,
                    new UnitCapability(Role.Armor, true, false, false, false, false), accurate: false, strategicPriority: 2f, armorTier: 3),
            };
            var objs = CommanderBrain.GenerateObjectives(fuzzy, new List<Objective>(), new BrainConfig(),
                new Vec3(0, 0, 0), new Doctrine());
            Assert.Contains(objs, o => o.Kind == ObjectiveKind.Recon);
        }

        [Fact]
        public void Threat_to_home_base_triggers_a_DefendArea()
        {
            var state = new CommanderState { HomeBase = new Vec3(1000f, 0f, 1000f) };
            var nearHome = new List<EnemyView>
            {
                Enemy("e1", 1600f, 0f, Role.Armor, false, UnitClass.GroundVehicle, 2f), // ~700m from home < DefendRadius
            };
            var snap = new WorldSnapshot(new List<UnitView>(), nearHome, 0f, null, 0f);
            CommanderBrain.Tick(snap, state);
            Assert.Contains(state.Objectives, o => o.Kind == ObjectiveKind.DefendArea);
        }

        [Fact]
        public void Unset_home_base_does_not_spawn_a_defense()
        {
            var state = new CommanderState(); // HomeBase defaults to origin = "unknown"
            var farEnemies = new List<EnemyView>
            {
                Enemy("e1", 50000f, 0f, Role.Armor, false, UnitClass.GroundVehicle, 2f),
            };
            var snap = new WorldSnapshot(new List<UnitView>(), farEnemies, 0f, null, 0f);
            CommanderBrain.Tick(snap, state);
            Assert.DoesNotContain(state.Objectives, o => o.Kind == ObjectiveKind.DefendArea);
        }

        [Fact]
        public void Defense_wins_the_last_objective_slot_when_the_cap_is_tight()
        {
            // With a single auto slot, a threatened home wins it over a higher-count offensive
            // pocket (defence is funded before attacks WITHIN the cap — defence is prepended before Take(room)).
            var cfg = new BrainConfig { MaxAutoObjectives = 1 };
            var state = new CommanderState(null, null, cfg) { HomeBase = new Vec3(2000f, 0f, 2000f) };
            var known = new List<EnemyView>
            {
                Enemy("a1", 20000f, 0f, Role.Armor, false, UnitClass.GroundVehicle, 3f),  // big offensive pocket far out
                Enemy("a2", 20100f, 0f, Role.Armor, false, UnitClass.GroundVehicle, 3f),
                Enemy("r1", 2500f, 0f, Role.Armor, false, UnitClass.GroundVehicle, 2f),   // raider on home
            };
            CommanderBrain.Tick(new WorldSnapshot(new List<UnitView>(), known, 0f, null, 0f), state);
            var autos = state.Objectives.Where(o => o.Source == ObjectiveSource.Auto).ToList();
            Assert.Single(autos);
            Assert.Equal(ObjectiveKind.DefendArea, autos[0].Kind);
        }

        [Fact]
        public void Mixed_battlefield_yields_more_than_one_objective_kind()
        {
            var state = new CommanderState { HomeBase = new Vec3(0f, 0f, 0f) };
            // Home must be non-origin to count as known; place it away and put a raiding force on it.
            state.HomeBase = new Vec3(2000f, 0f, 2000f);
            var known = new List<EnemyView>
            {
                // armor pocket far out -> CapturePoint
                Enemy("a1", 20000f, 0f, Role.Armor, false, UnitClass.GroundVehicle, 2f),
                Enemy("a2", 20100f, 0f, Role.Armor, false, UnitClass.GroundVehicle, 2f),
                // SAM pocket far out -> DestroyTarget
                Enemy("s1", -20000f, 0f, Role.GroundAirDefense, true, UnitClass.GroundVehicle, 3f),
                Enemy("s2", -20100f, 0f, Role.GroundAirDefense, true, UnitClass.GroundVehicle, 3f),
                // raid on home -> DefendArea
                Enemy("r1", 2500f, 0f, Role.Armor, false, UnitClass.GroundVehicle, 2f),
            };
            var snap = new WorldSnapshot(new List<UnitView>(), known, 0f, null, 0f);
            CommanderBrain.Tick(snap, state);
            var kinds = state.Objectives.Select(o => o.Kind).Distinct().ToList();
            Assert.True(kinds.Count >= 2, "expected a mix of objective kinds, got: " + string.Join(",", kinds));
            Assert.Contains(ObjectiveKind.DefendArea, kinds);
        }
    }
}
