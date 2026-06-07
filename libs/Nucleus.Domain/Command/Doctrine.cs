namespace Nucleus.Core.Command
{
    /// <summary>
    /// Tunable command rules. A single <see cref="RiskTolerance"/> master (0 = cautious, 1 = aggressive)
    /// derives the combined-arms thresholds in P2 (air-superiority ratio, residual-AD tolerance, soften
    /// fraction) and the force-sizing ratio. Pure data; layered per Commander/Operation/Squad later.
    /// </summary>
    public sealed class Doctrine
    {
        public float RiskTolerance { get; set; } = 0.5f;

        /// <summary>How many times the known threat an offensive force should outnumber (force-sizing).</summary>
        public float ForceRatio { get; set; } = 1.5f;

        // Combined-arms gate thresholds, derived from RiskTolerance (cautious 0 .. aggressive 1).
        /// <summary>Friendly-fighter : enemy-air ratio required for air superiority (2:1 cautious → 1:1 aggressive).</summary>
        public float AirSuperiorityRatio => Lerp(2.0f, 1.0f);
        /// <summary>Enemy SAM/AAA count tolerated before strike/assault may proceed (0 cautious → 2 aggressive).</summary>
        public int MaxResidualAirDefense => (int)Lerp(0f, 2f);
        /// <summary>Fraction of the initial armor+air-defense that must be destroyed before assault (0.75 → 0.25).</summary>
        public float SoftenThreshold => Lerp(0.75f, 0.25f);

        private float Lerp(float cautious, float aggressive)
        {
            float t = RiskTolerance < 0f ? 0f : RiskTolerance > 1f ? 1f : RiskTolerance;
            return cautious + (aggressive - cautious) * t;
        }

        /// <summary>Drive this doctrine from a personality genome: Aggression → RiskTolerance (all the gate
        /// thresholds), Caution → ForceRatio (1.0 reckless .. 2.0 cautious). The default genome reproduces the
        /// stock 0.5 / 1.5 exactly, so unpersonalized commanders behave as before. Returns this for chaining.</summary>
        public Doctrine ApplyGenome(CommanderGenome g)
        {
            if (g != null)
            {
                RiskTolerance = g.Aggression < 0f ? 0f : g.Aggression > 1f ? 1f : g.Aggression;
                float caution = g.Caution < 0f ? 0f : g.Caution > 1f ? 1f : g.Caution;
                ForceRatio = 1.0f + caution; // 1.0 (reckless) .. 2.0 (cautious); 1.5 at the neutral 0.5
            }
            return this;
        }

        public static Doctrine FromGenome(CommanderGenome g) => new Doctrine().ApplyGenome(g);
    }
}
