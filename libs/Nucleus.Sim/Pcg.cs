namespace Nucleus.Sim
{
    /// <summary>
    /// Small deterministic PRNG (xorshift64*). Used instead of System.Random because its sequence is NOT
    /// guaranteed stable across .NET versions — determinism tests must be reproducible forever.
    /// </summary>
    public sealed class Pcg
    {
        private ulong _s;

        public Pcg(ulong seed) { _s = seed == 0UL ? 0x9E3779B97F4A7C15UL : seed; }

        public ulong NextULong()
        {
            _s ^= _s >> 12;
            _s ^= _s << 25;
            _s ^= _s >> 27;
            return _s * 0x2545F4914F6CDD1DUL;
        }

        /// <summary>Uniform float in [0,1).</summary>
        public float NextFloat() => (NextULong() >> 40) * (1.0f / 16777216.0f);

        public float Range(float a, float b) => a + (b - a) * NextFloat();
    }
}
