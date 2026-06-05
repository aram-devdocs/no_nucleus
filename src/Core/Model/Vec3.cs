namespace CommanderLayer.Core.Model
{
    /// <summary>
    /// Unity-free 3D position value type. The Core layer must not reference UnityEngine, so adapters
    /// convert the game's GlobalPosition/Vector3 to and from this at the boundary.
    /// World axes match the game: x = east, y = up, z = north. The map is the horizontal (x,z) plane.
    /// </summary>
    public readonly struct Vec3
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
