using System.Collections.Generic;
using System.Linq;

namespace CommanderLayer.Core.Command
{
    /// <summary>
    /// Turns a force gap ("we are short 2× Armor, 1× AirDefense") into a concrete shopping list of convoy
    /// purchases, bounded by available funds. Greedy cheapest-set cover: each step picks the affordable
    /// option that fills the most still-missing units per unit of cost, then subtracts what it delivers.
    /// Deterministic (ties broken by name). Never overspends. Pure, Unity-free.
    /// </summary>
    public static class ProductionPlanner
    {
        public static IReadOnlyList<ConvoyOption> Plan(Composition gap, ConvoyCatalog catalog, float funds)
        {
            var picks = new List<ConvoyOption>();
            if (gap == null || catalog == null) return picks;

            // Working copy of what's still missing — drained as we buy.
            var remaining = new Composition();
            foreach (var kv in gap.Items)
                if (kv.Value > 0) remaining.Set(kv.Key, kv.Value);

            float budget = funds;

            while (remaining.Total > 0)
            {
                ConvoyOption best = null;
                int bestFill = 0;
                float bestScore = 0f;

                foreach (var opt in catalog.Options)
                {
                    if (opt.Cost > budget) continue;          // unaffordable now
                    int fill = Covers(opt, remaining);        // how much of the gap this option actually clears
                    if (fill <= 0) continue;                  // useless — delivers nothing we still need

                    // Value per cost; free options (cost <= 0) rank purely by how much they fill.
                    float score = opt.Cost > 0f ? fill / opt.Cost : float.MaxValue;

                    if (best == null
                        || score > bestScore
                        || (score == bestScore && fill > bestFill)
                        || (score == bestScore && fill == bestFill
                            && string.CompareOrdinal(opt.Name, best.Name) < 0))
                    {
                        best = opt;
                        bestFill = fill;
                        bestScore = score;
                    }
                }

                if (best == null) break;                      // nothing affordable + useful remains

                picks.Add(best);
                budget -= best.Cost;
                Subtract(remaining, best.Delivers);
            }

            return picks;
        }

        /// <summary>How many still-missing units an option clears (capped at the remaining gap).</summary>
        private static int Covers(ConvoyOption opt, Composition remaining)
        {
            int sum = 0;
            foreach (var kv in opt.Delivers.Items)
            {
                int need = remaining.Get(kv.Key);
                if (need > 0) sum += System.Math.Min(need, kv.Value);
            }
            return sum;
        }

        /// <summary>Drain delivered counts from the remaining gap (never below zero).</summary>
        private static void Subtract(Composition remaining, Composition delivers)
        {
            foreach (var kv in delivers.Items)
            {
                int left = remaining.Get(kv.Key) - kv.Value;
                remaining.Set(kv.Key, left > 0 ? left : 0);
            }
        }
    }
}
