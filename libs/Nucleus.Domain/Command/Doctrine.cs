namespace Nucleus.Core.Command
{
    /// <summary>Tunable command rules. A single <see cref="RiskTolerance"/> master (0 = cautious, 1 = aggressive)
    /// derives the combined-arms phase thresholds (air-superiority ratio, residual-AD tolerance, soften fraction)
    /// and the force-sizing ratio.</summary>
    public sealed class Doctrine
    {
        public float RiskTolerance { get; set; } = 0.5f;

        /// <summary>How many times the known threat an offensive force should outnumber (force-sizing).</summary>
        public float ForceRatio { get; set; } = 1.5f;

        // Personality knobs, all NEUTRAL at their default so a stock doctrine (or an all-0.5 genome) behaves as
        // before. ApplyGenome sets each from one gene; the brain multiplies the relevant value by these.
        /// <summary>Scales home-defence priority (DefenseBias). 1.0 = stock.</summary>
        public float DefendWeight { get; set; } = 1.0f;
        /// <summary>Scales the concurrent auto-objective cap (FocusBroad). 1.0 = stock (focus &lt;1, broad &gt;1).</summary>
        public float ObjectiveSpread { get; set; } = 1.0f;
        /// <summary>Scales offensive coverage reach (Overextension). 1.0 = stock.</summary>
        public float Reach { get; set; } = 1.0f;
        /// <summary>Scales recon-objective propensity (ReconBias). 1.0 = stock.</summary>
        public float ReconWeight { get; set; } = 1.0f;
        /// <summary>Air-vs-ground objective bias (AirGroundPref): 0 ground .. 0.5 neutral .. 1 air.</summary>
        public float AirPreference { get; set; } = 0.5f;
        /// <summary>Production budget fraction the Game layer commits per cycle (EconomyBias). 1.0 = stock.</summary>
        public float EconomyWeight { get; set; } = 1.0f;
        /// <summary>Phase-advance eagerness (Tempo): 0.5 neutral; higher commits the assault on less softening.</summary>
        public float Tempo { get; set; } = 0.5f;

        // Combined-arms gate thresholds, derived from RiskTolerance (cautious 0 .. aggressive 1).
        /// <summary>Friendly-fighter : enemy-air ratio required for air superiority (2:1 cautious → 1:1 aggressive).</summary>
        public float AirSuperiorityRatio => Lerp(2.0f, 1.0f);
        /// <summary>Enemy SAM/AAA count tolerated before strike/assault may proceed (0 cautious → 2 aggressive).</summary>
        public int MaxResidualAirDefense => (int)Lerp(0f, 2f);
        /// <summary>Fraction of the initial armor+air-defense to destroy before assault (0.75 → 0.25), pulled in or
        /// out by Tempo (0.5 = no change). Clamped so it always stays a meaningful gate.</summary>
        public float SoftenThreshold
        {
            get
            {
                float baseFrac = Lerp(0.75f, 0.25f);
                float tempoFactor = 1.5f - Tempo;   // Tempo 0.5 → 1.0 (stock); higher → less softening required
                float v = baseFrac * tempoFactor;
                return v < 0.05f ? 0.05f : v > 0.95f ? 0.95f : v;
            }
        }

        private float Lerp(float cautious, float aggressive)
        {
            float t = RiskTolerance < 0f ? 0f : RiskTolerance > 1f ? 1f : RiskTolerance;
            return cautious + (aggressive - cautious) * t;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        /// <summary>Drive this doctrine from a personality genome: Aggression → RiskTolerance (all the gate
        /// thresholds), Caution → ForceRatio (1.0 reckless .. 2.0 cautious). The default genome reproduces the
        /// stock 0.5 / 1.5 exactly, so unpersonalized commanders behave as before. Returns this for chaining.</summary>
        public Doctrine ApplyGenome(CommanderGenome g)
        {
            if (g != null)
            {
                RiskTolerance = Clamp01(g.Aggression);
                ForceRatio = 1.0f + Clamp01(g.Caution); // 1.0 (reckless) .. 2.0 (cautious); 1.5 at the neutral 0.5
                // Each knob is centered so a 0.5 gene yields the neutral default (stock behavior preserved).
                DefendWeight = 0.5f + Clamp01(g.DefenseBias);     // 0.5 .. 1.5, 1.0 at 0.5
                ObjectiveSpread = 0.5f + Clamp01(g.FocusBroad);   // 0.5 .. 1.5
                Reach = 0.5f + Clamp01(g.Overextension);          // 0.5 .. 1.5
                ReconWeight = 0.5f + Clamp01(g.ReconBias);        // 0.5 .. 1.5
                EconomyWeight = 0.5f + Clamp01(g.EconomyBias);    // 0.5 .. 1.5
                AirPreference = Clamp01(g.AirGroundPref);         // 0 ground .. 1 air, 0.5 neutral
                Tempo = Clamp01(g.Tempo);                         // 0.5 neutral
            }
            return this;
        }

        public static Doctrine FromGenome(CommanderGenome g) => new Doctrine().ApplyGenome(g);
    }
}
