using System.Linq;
using Nucleus.Sim;
using Xunit;

namespace Nucleus.Sim.Tests
{
    /// <summary>The self-play evolution loop must be fully deterministic (same seed ⇒ identical genepool),
    /// so its report is reproducible and the feature never introduces nondeterminism.</summary>
    public class EvolveTests
    {
        [Fact]
        public void Evolve_is_deterministic_for_a_given_seed()
        {
            var a = Evolver.Run(seed: 1337UL, generations: 3, matchTicks: 120);
            var b = Evolver.Run(seed: 1337UL, generations: 3, matchTicks: 120);
            Assert.Equal(a.Final.Count, b.Final.Count);
            for (int i = 0; i < a.Final.Count; i++)
            {
                Assert.Equal(a.Final[i].Genome.Archetype, b.Final[i].Genome.Archetype);
                Assert.Equal(a.Final[i].Fitness, b.Final[i].Fitness);
                Assert.Equal(a.Final[i].Genome.Aggression, b.Final[i].Genome.Aggression);
            }
            Assert.Equal(a.GenerationLog, b.GenerationLog);
        }

        [Fact]
        public void Evolve_produces_a_ranked_nonempty_population()
        {
            var r = Evolver.Run(seed: 7UL, generations: 2, matchTicks: 120);
            Assert.NotEmpty(r.Final);
            // Ranked best-first.
            for (int i = 1; i < r.Final.Count; i++)
                Assert.True(r.Final[i - 1].Fitness >= r.Final[i].Fitness);
        }
    }
}
