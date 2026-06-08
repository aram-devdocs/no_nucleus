using System.Collections.Generic;

namespace Nucleus.Core.Model
{
    /// <summary>
    /// Pure geometric reductions over a unit roster. SSOT for "where is this force" computations the
    /// commander layer uses (e.g. a side's home-base reference for defensive reasoning).
    /// </summary>
    public static class RosterGeometry
    {
        /// <summary>
        /// Average position of a roster — a cheap "home base" / force-centre reference. Returns the origin
        /// for an empty or null roster (the convention every caller relies on).
        /// </summary>
        public static Vec3 Centroid(IReadOnlyList<UnitView> roster)
        {
            if (roster == null || roster.Count == 0) return new Vec3(0f, 0f, 0f);
            float x = 0f, y = 0f, z = 0f;
            foreach (var u in roster) { x += u.Position.X; y += u.Position.Y; z += u.Position.Z; }
            float inv = 1f / roster.Count;
            return new Vec3(x * inv, y * inv, z * inv);
        }
    }
}
