using System.Collections.Generic;
using System.Linq;
using Nucleus.Core.Command;
using Nucleus.Core.Model;
using Xunit;

namespace Nucleus.Sim.Tests
{
    /// <summary>The AI narrates its decisions to the battle feed so the player can SEE what it's doing.</summary>
    public class NarrationTests
    {
        private static EnemyView Enemy(string id, float x, float z, Role role, bool ad)
            => new EnemyView(id, new Vec3(x, 0f, z), UnitClass.GroundVehicle,
                new UnitCapability(role, role == Role.Armor, ad, false, false, ad),
                accurate: true, strategicPriority: 2f, armorTier: 3);

        [Fact]
        public void AppendDistinct_suppresses_consecutive_duplicates()
        {
            var log = new BattleLog();
            log.AppendDistinct(new ReportEvent(0f, ReportKind.PhaseChanged, "DestroyTarget: scouting"));
            log.AppendDistinct(new ReportEvent(1f, ReportKind.PhaseChanged, "DestroyTarget: scouting")); // dup
            Assert.Equal(1, log.Count);
            log.AppendDistinct(new ReportEvent(2f, ReportKind.PhaseChanged, "DestroyTarget: ground assault going in"));
            Assert.Equal(2, log.Count);
        }

        [Fact]
        public void Brain_narrates_objective_creation_with_a_reason()
        {
            var state = new CommanderState();
            var known = new List<EnemyView>
            {
                Enemy("a1", 30000f, 0f, Role.Armor, false),
                Enemy("a2", 30100f, 0f, Role.Armor, false),
            };
            var snap = new WorldSnapshot(new List<UnitView>(), known, 0f, null, 0f);
            CommanderBrain.Tick(snap, state);
            Assert.Contains(state.Log.Recent(20),
                e => e.Kind == ReportKind.ObjectiveAdded && e.Text.StartsWith("AI:"));
        }

        [Fact]
        public void Defense_bark_fires_when_home_is_threatened()
        {
            var state = new CommanderState { HomeBase = new Vec3(5000f, 0f, 5000f) };
            var raid = new List<EnemyView> { Enemy("r1", 5400f, 0f, Role.Armor, false) };
            var snap = new WorldSnapshot(new List<UnitView>(), raid, 0f, null, 0f);
            CommanderBrain.Tick(snap, state);
            Assert.Contains(state.Log.Recent(20),
                e => e.Kind == ReportKind.ObjectiveAdded && e.Text.Contains("defending HQ"));
        }
    }
}
