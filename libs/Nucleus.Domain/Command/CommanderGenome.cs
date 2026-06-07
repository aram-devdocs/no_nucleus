using System.Collections.Generic;

namespace Nucleus.Core.Command
{
    /// <summary>
    /// A commander's personality as a vector of [0,1] genes — what makes one AI commander feel like a distinct
    /// character (an aggressive armor-rusher vs a patient, scouting turtle). Pure data; deterministic. Today the
    /// brain consumes Aggression (→ Doctrine.RiskTolerance) and Caution (→ Doctrine.ForceRatio); the remaining
    /// genes are carried for the upcoming utility/evolution passes (and are already exercised by tests).
    /// </summary>
    public sealed class CommanderGenome
    {
        public float Aggression { get; set; } = 0.5f;     // willingness to attack / risk
        public float Caution { get; set; } = 0.5f;        // force-sizing & gate conservatism
        public float ReconBias { get; set; } = 0.3f;      // scout before committing
        public float DefenseBias { get; set; } = 0.4f;    // weight on defending home/assets
        public float EconomyBias { get; set; } = 0.4f;    // husband funds / reinforce
        public float AirGroundPref { get; set; } = 0.5f;  // 0 ground-first .. 1 air-first
        public float FocusBroad { get; set; } = 0.5f;     // 0 focus-fire .. 1 broad-front
        public float Overextension { get; set; } = 0.5f;  // reach for distant targets
        public float Tempo { get; set; } = 0.5f;          // phase-advance eagerness

        /// <summary>The named archetype this genome was drawn from (for player-facing "enemy commander" labels).</summary>
        public string Archetype { get; set; } = "Balanced";

        /// <summary>The neutral genome — maps to today's exact Doctrine defaults (RiskTolerance 0.5, ForceRatio 1.5),
        /// so the brain behaves identically when no personality is assigned.</summary>
        public static CommanderGenome Default => new CommanderGenome();

        public CommanderGenome Clone() => new CommanderGenome
        {
            Aggression = Aggression, Caution = Caution, ReconBias = ReconBias, DefenseBias = DefenseBias,
            EconomyBias = EconomyBias, AirGroundPref = AirGroundPref, FocusBroad = FocusBroad,
            Overextension = Overextension, Tempo = Tempo, Archetype = Archetype
        };
    }

    /// <summary>
    /// Builds deterministic personality genomes from a stable seed (campaign + faction name) — same inputs always
    /// produce the same commander, different factions diverge. Picks a named archetype and jitters its genes a
    /// little so two commanders of the same archetype still differ. Pure; reproducible across runs + save/resume.
    /// </summary>
    public static class GenomeFactory
    {
        // Hand-authored archetype base vectors. Order is stable (archetype pick = hash % count).
        private static readonly CommanderGenome[] Archetypes =
        {
            new CommanderGenome { Archetype = "Iron Hammer", Aggression = 0.85f, Caution = 0.20f, ReconBias = 0.15f, DefenseBias = 0.20f, EconomyBias = 0.30f, AirGroundPref = 0.30f, FocusBroad = 0.20f, Overextension = 0.55f, Tempo = 0.70f },
            new CommanderGenome { Archetype = "Fabian",      Aggression = 0.30f, Caution = 0.80f, ReconBias = 0.70f, DefenseBias = 0.70f, EconomyBias = 0.70f, AirGroundPref = 0.45f, FocusBroad = 0.70f, Overextension = 0.25f, Tempo = 0.35f },
            new CommanderGenome { Archetype = "Sky Marshal",  Aggression = 0.60f, Caution = 0.45f, ReconBias = 0.60f, DefenseBias = 0.35f, EconomyBias = 0.45f, AirGroundPref = 0.85f, FocusBroad = 0.50f, Overextension = 0.55f, Tempo = 0.60f },
            new CommanderGenome { Archetype = "Bastion",      Aggression = 0.25f, Caution = 0.75f, ReconBias = 0.40f, DefenseBias = 0.85f, EconomyBias = 0.70f, AirGroundPref = 0.45f, FocusBroad = 0.45f, Overextension = 0.20f, Tempo = 0.35f },
            new CommanderGenome { Archetype = "Jackal",       Aggression = 0.70f, Caution = 0.25f, ReconBias = 0.45f, DefenseBias = 0.25f, EconomyBias = 0.35f, AirGroundPref = 0.50f, FocusBroad = 0.30f, Overextension = 0.80f, Tempo = 0.80f },
            new CommanderGenome { Archetype = "Steamroller",  Aggression = 0.60f, Caution = 0.55f, ReconBias = 0.30f, DefenseBias = 0.40f, EconomyBias = 0.70f, AirGroundPref = 0.40f, FocusBroad = 0.90f, Overextension = 0.50f, Tempo = 0.55f },
        };

        public static IReadOnlyList<CommanderGenome> AllArchetypes => Archetypes;

        /// <summary>Deterministic genome for a commander, seeded by campaign id + faction name. Picks an archetype
        /// by hash and jitters each gene ±0.12 (clamped), so it's reproducible yet distinct per faction.</summary>
        public static CommanderGenome ForCommander(string campaignSeed, string faction)
        {
            ulong seed = Fnv1a.Combine(Fnv1a.Hash(campaignSeed ?? ""), faction ?? "");
            var rng = new DeterministicRng(seed);
            var baseGenome = Archetypes[(int)(seed % (ulong)Archetypes.Length)];
            var g = baseGenome.Clone();
            g.Aggression = Jitter(rng, g.Aggression);
            g.Caution = Jitter(rng, g.Caution);
            g.ReconBias = Jitter(rng, g.ReconBias);
            g.DefenseBias = Jitter(rng, g.DefenseBias);
            g.EconomyBias = Jitter(rng, g.EconomyBias);
            g.AirGroundPref = Jitter(rng, g.AirGroundPref);
            g.FocusBroad = Jitter(rng, g.FocusBroad);
            g.Overextension = Jitter(rng, g.Overextension);
            g.Tempo = Jitter(rng, g.Tempo);
            return g;
        }

        private static float Jitter(DeterministicRng rng, float v)
        {
            float x = v + rng.Range(-0.12f, 0.12f);
            return x < 0f ? 0f : x > 1f ? 1f : x;
        }
    }
}
