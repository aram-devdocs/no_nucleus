namespace Nucleus.Core.Model
{
    /// <summary>
    /// Unity-free color (components in 0..1). Keeps Core free of UnityEngine.Color; the UI layer
    /// converts this to a UnityEngine.Color at the boundary.
    /// </summary>
    public readonly struct ColorRgba : System.IEquatable<ColorRgba>
    {
        public readonly float R;
        public readonly float G;
        public readonly float B;
        public readonly float A;

        public ColorRgba(float r, float g, float b, float a = 1f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public static ColorRgba White => new ColorRgba(1f, 1f, 1f);

        public bool Equals(ColorRgba other) => R == other.R && G == other.G && B == other.B && A == other.A;
        public override bool Equals(object obj) => obj is ColorRgba c && Equals(c);
        public static bool operator ==(ColorRgba a, ColorRgba b) => a.Equals(b);
        public static bool operator !=(ColorRgba a, ColorRgba b) => !a.Equals(b);

        /// <summary>Deterministic hash (FNV-1a over the raw float bits) — never <c>HashCode.Combine</c> (per-process seed).</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                const uint prime = 16777619u; uint h = 2166136261u;
                h = (h ^ (uint)System.BitConverter.SingleToInt32Bits(R)) * prime;
                h = (h ^ (uint)System.BitConverter.SingleToInt32Bits(G)) * prime;
                h = (h ^ (uint)System.BitConverter.SingleToInt32Bits(B)) * prime;
                h = (h ^ (uint)System.BitConverter.SingleToInt32Bits(A)) * prime;
                return (int)h;
            }
        }

        public override string ToString() => $"({R:0.##}, {G:0.##}, {B:0.##}, {A:0.##})";
    }
}
