namespace Nucleus.Core.Command
{
    /// <summary>Stable FNV-1a 64-bit hash. Used to seed deterministic per-commander personalities from strings —
    /// NEVER string.GetHashCode() (randomized per-process in .NET 5+, so it would not reproduce across runs).</summary>
    public static class Fnv1a
    {
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static ulong Hash(string s) => Combine(Offset, s);

        public static ulong Combine(ulong seed, string s)
        {
            ulong h = seed;
            if (s != null)
                foreach (char c in s) { h ^= c; h *= Prime; }
            return h;
        }
    }

    /// <summary>A small, fully deterministic PRNG (xorshift64*). Pure — no clock, no Unity. Same seed ⇒ same
    /// stream, so personality genomes are reproducible across runs and save/resume.</summary>
    public sealed class DeterministicRng
    {
        private ulong _s;

        public DeterministicRng(ulong seed) { _s = seed == 0UL ? 0x9E3779B97F4A7C15UL : seed; }

        public ulong NextULong()
        {
            _s ^= _s >> 12;
            _s ^= _s << 25;
            _s ^= _s >> 27;
            return _s * 0x2545F4914F6CDD1DUL;
        }

        /// <summary>A float in [0,1).</summary>
        public float NextFloat() => (NextULong() >> 11) * (1.0f / 9007199254740992.0f);

        /// <summary>A float in [min,max).</summary>
        public float Range(float min, float max) => min + (max - min) * NextFloat();
    }
}
