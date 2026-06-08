namespace Nucleus.Core.Model
{
    /// <summary>
    /// Unity-free 3D position value type. The Core layer must not reference UnityEngine, so adapters
    /// convert the game's GlobalPosition/Vector3 to and from this at the boundary.
    /// World axes match the game: x = east, y = up, z = north. The map is the horizontal (x,z) plane.
    /// </summary>
    public readonly struct Vec3 : System.IEquatable<Vec3>
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public bool Equals(Vec3 other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is Vec3 v && Equals(v);
        public static bool operator ==(Vec3 a, Vec3 b) => a.Equals(b);
        public static bool operator !=(Vec3 a, Vec3 b) => !a.Equals(b);

        /// <summary>Deterministic hash (FNV-1a over the raw float bits) — never <c>HashCode.Combine</c>, whose
        /// per-process seed would break the byte-identical save/resume invariant if a Vec3 keyed a dictionary.</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                const uint prime = 16777619u; uint h = 2166136261u;
                h = (h ^ (uint)System.BitConverter.SingleToInt32Bits(X)) * prime;
                h = (h ^ (uint)System.BitConverter.SingleToInt32Bits(Y)) * prime;
                h = (h ^ (uint)System.BitConverter.SingleToInt32Bits(Z)) * prime;
                return (int)h;
            }
        }

        /// <summary>Full 3D distance.</summary>
        public float DistanceTo(Vec3 other)
        {
            float dx = X - other.X;
            float dy = Y - other.Y;
            float dz = Z - other.Z;
            return (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>Distance ignoring altitude — the meaningful distance for map tasking.</summary>
        public float HorizontalDistanceTo(Vec3 other)
        {
            float dx = X - other.X;
            float dz = Z - other.Z;
            return (float)System.Math.Sqrt(dx * dx + dz * dz);
        }

        public override string ToString() => $"({X:0}, {Y:0}, {Z:0})";
    }
}
