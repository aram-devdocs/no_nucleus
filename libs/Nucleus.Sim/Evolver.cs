using System.Collections.Generic;
using System.Linq;
using Nucleus.Core.Command;

namespace Nucleus.Sim
{
    /// <summary>One scored commander personality in the genepool.</summary>
    public sealed class GenomeScore
    {
        public CommanderGenome Genome;
        public float Fitness;
        public int Generation;   // generation this genome was last (re)created in
    }

    /// <summary>The result of an evolution run: the final population scored best-first, plus a per-generation log.</summary>
    public sealed class EvolveResult
    {
        public List<GenomeScore> Final = new List<GenomeScore>();   // sorted best-first
        public List<string> GenerationLog = new List<string>();     // one line per generation
    }

    /// <summary>
    /// A fully DETERMINISTIC genetic self-play loop: a population of commander genomes plays round-robin matches
    /// in the headless <see cref="DualSimWorld"/>, is scored by who survives, and is evolved (elitism + mutation)
    /// over generations. Same seed ⇒ identical genepool. Pure — no clock, no Unity. The output is a REVIEWABLE
    /// report (tools/Nucleus.Evolve writes it to disk); evolved genomes are NOT auto-applied to shipped gameplay
    /// (the coarse sim's fitness is a proxy, not ground truth — hand-authored archetypes remain the default).
    /// </summary>
    public static class Evolver
    {
        public static EvolveResult Run(ulong seed, int generations, int matchTicks = 250)
        {
            var result = new EvolveResult();
            // Seed the population from the hand-authored archetypes.
            var pop = GenomeFactory.AllArchetypes.Select(g => g.Clone()).ToList();
            int n = pop.Count;
            ulong[] matchSeeds = { 1UL, 2UL };

            for (int gen = 0; gen < generations; gen++)
            {
                var scores = new float[n];
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                    {
                        if (i == j) continue;
                        foreach (var ms in matchSeeds)
                            scores[i] += MatchScore(pop[i], pop[j], ms, matchTicks);
                    }

                var ranked = Enumerable.Range(0, n)
                    .Select(i => new GenomeScore { Genome = pop[i], Fitness = scores[i] })
                    .OrderByDescending(s => s.Fitness)
                    .ThenBy(s => s.Genome.Archetype)               // stable tie-break → deterministic
                    .ToList();

                var best = ranked[0];
                result.GenerationLog.Add($"gen {gen}: best='{best.Genome.Archetype}' fitness={best.Fitness:0.0} " +
                    $"agg={best.Genome.Aggression:0.00} cau={best.Genome.Caution:0.00}");

                if (gen == generations - 1)
                {
                    foreach (var s in ranked) s.Generation = gen;
                    result.Final = ranked;
                    break;
                }

                // Next generation: keep the top half (elitism), refill the rest by mutating an elite.
                int elite = (n + 1) / 2;
                var next = new List<CommanderGenome>(n);
                for (int k = 0; k < elite; k++) next.Add(ranked[k].Genome.Clone());
                var rng = new DeterministicRng(Fnv1a.Combine(seed, "gen" + gen));
                for (int k = elite; k < n; k++)
                {
                    var parent = ranked[k % elite].Genome;
                    next.Add(Mutate(parent, rng));
                }
                pop = next;
            }
            return result;
        }

        // A match's contribution to A's fitness: +1 if A has more units alive at the end, -1 if fewer, 0 tie.
        private static float MatchScore(CommanderGenome a, CommanderGenome b, ulong seed, int ticks)
        {
            var (ua, ub) = Scenarios.DualForces();
            var sa = new CommanderState(null, Doctrine.FromGenome(a), null);
            var sb = new CommanderState(null, Doctrine.FromGenome(b), null);
            var r = new DualSimWorld(ua, ub, seed, sa, sb).Run(ticks);
            int aEnd = r.AAlive.Count > 0 ? r.AAlive[r.AAlive.Count - 1] : 0;
            int bEnd = r.BAlive.Count > 0 ? r.BAlive[r.BAlive.Count - 1] : 0;
            return aEnd > bEnd ? 1f : aEnd < bEnd ? -1f : 0f;
        }

        private static CommanderGenome Mutate(CommanderGenome p, DeterministicRng rng)
        {
            var g = p.Clone();
            g.Aggression = Jit(rng, g.Aggression);
            g.Caution = Jit(rng, g.Caution);
            g.ReconBias = Jit(rng, g.ReconBias);
            g.DefenseBias = Jit(rng, g.DefenseBias);
            g.EconomyBias = Jit(rng, g.EconomyBias);
            g.AirGroundPref = Jit(rng, g.AirGroundPref);
            g.FocusBroad = Jit(rng, g.FocusBroad);
            g.Overextension = Jit(rng, g.Overextension);
            g.Tempo = Jit(rng, g.Tempo);
            g.Archetype = p.Archetype + "*";   // mark as a mutated descendant
            return g;
        }

        private static float Jit(DeterministicRng rng, float v)
        {
            float x = v + rng.Range(-0.1f, 0.1f);
            return x < 0f ? 0f : x > 1f ? 1f : x;
        }
    }
}
