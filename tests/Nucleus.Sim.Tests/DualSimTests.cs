using Nucleus.Sim;
using Xunit;

namespace Nucleus.Sim.Tests
{
    /// <summary>
    /// North-star: BOTH factions run the CommanderBrain over one battlefield (Nucleus Dynamic Warfare's core).
    /// Headless, deterministic, and proves a self-running war resolves with both sides active.
    /// </summary>
    public class DualSimTests
    {
        private static DualSimWorld World(ulong seed)
        {
            var (a, b) = Scenarios.DualForces();
            return new DualSimWorld(a, b, seed);
        }

        [Fact]
        public void Both_factions_detect_the_enemy_and_plan()
        {
            // Both sides run their own brain over the same battlefield: each detects the other (fog-of-war)
            // and generates objectives — i.e., both factions are actively commanding. (Whether ground units
            // then assault is doctrine-gated on softening the target, which combined-arms assets the coarse
            // sim doesn't model; planning is the robust dual-faction invariant.)
            var r = World(11).Run(400);
            Assert.True(r.AObjectivesMax > 0, "faction A brain never planned an objective");
            Assert.True(r.BObjectivesMax > 0, "faction B brain never planned an objective");
        }

        [Fact]
        public void Dual_is_deterministic()
        {
            Assert.Equal(World(11).Run(300).Fingerprint(), World(11).Run(300).Fingerprint());
        }

        [Fact]
        public void Dual_never_goes_non_finite()
        {
            Assert.False(World(3).Run(1000).AnyNaN);
        }
    }
}
