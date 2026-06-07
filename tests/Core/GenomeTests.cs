using Nucleus.Core.Command;
using Xunit;

namespace Nucleus.Tests
{
    /// <summary>WS2 — commander personalities are deterministic (reproducible across runs + save/resume) and the
    /// default genome reproduces the stock doctrine exactly (so existing behavior is unchanged).</summary>
    public class GenomeTests
    {
        [Fact]
        public void Same_seed_and_faction_produce_an_identical_genome()
        {
            var a = GenomeFactory.ForCommander("nucleus-war", "Boscali");
            var b = GenomeFactory.ForCommander("nucleus-war", "Boscali");
            Assert.Equal(a.Archetype, b.Archetype);
            Assert.Equal(a.Aggression, b.Aggression);
            Assert.Equal(a.Caution, b.Caution);
            Assert.Equal(a.Tempo, b.Tempo);
        }

        [Fact]
        public void Different_factions_diverge()
        {
            var a = GenomeFactory.ForCommander("nucleus-war", "Boscali");
            var b = GenomeFactory.ForCommander("nucleus-war", "Primeva");
            // At least one gene must differ (overwhelmingly likely; same archetype still jitters differently).
            bool differs = a.Archetype != b.Archetype
                || a.Aggression != b.Aggression || a.Caution != b.Caution || a.Tempo != b.Tempo;
            Assert.True(differs, "two different factions produced identical genomes");
        }

        [Fact]
        public void Genes_stay_in_range()
        {
            foreach (var f in new[] { "Boscali", "Primeva", "Redland", "Blueland", "X", "" })
            {
                var g = GenomeFactory.ForCommander("nucleus-war", f);
                Assert.InRange(g.Aggression, 0f, 1f);
                Assert.InRange(g.Caution, 0f, 1f);
                Assert.InRange(g.DefenseBias, 0f, 1f);
            }
        }

        [Fact]
        public void Default_genome_reproduces_the_stock_doctrine()
        {
            var stock = new Doctrine();                       // RiskTolerance 0.5, ForceRatio 1.5
            var fromDefault = Doctrine.FromGenome(CommanderGenome.Default);
            Assert.Equal(stock.RiskTolerance, fromDefault.RiskTolerance);
            Assert.Equal(stock.ForceRatio, fromDefault.ForceRatio);
        }

        [Fact]
        public void Genome_drives_risk_and_force_ratio()
        {
            var d = Doctrine.FromGenome(new CommanderGenome { Aggression = 0.9f, Caution = 0.2f });
            Assert.Equal(0.9f, d.RiskTolerance);
            Assert.Equal(1.2f, d.ForceRatio, 3);
            // An aggressive doctrine tolerates more residual air defense than a cautious one.
            var cautious = Doctrine.FromGenome(new CommanderGenome { Aggression = 0.1f, Caution = 0.9f });
            Assert.True(d.MaxResidualAirDefense >= cautious.MaxResidualAirDefense);
        }

        [Fact]
        public void Fnv1a_is_stable_and_not_runtime_random()
        {
            // FNV-1a of a known string is a compile-time constant — proves we are NOT using string.GetHashCode().
            Assert.Equal(0xAF63DC4C8601EC8CUL, Fnv1a.Hash("a"));
        }
    }
}
