using Nucleus.Core.Model;
using UnityEngine;

namespace Nucleus.Game
{
    /// <summary>Boundary conversions between engine types and the Unity-free Core value types.</summary>
    internal static class GameConvert
    {
        public static Vec3 ToVec3(GlobalPosition gp) => new Vec3(gp.x, gp.y, gp.z);

        public static GlobalPosition ToGlobal(Vec3 v) => new GlobalPosition(v.X, v.Y, v.Z);

        public static ColorRgba ToRgba(Color c) => new ColorRgba(c.r, c.g, c.b, c.a);
    }
}
