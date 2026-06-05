using System.Collections.Generic;
using CommanderLayer.Core.Model;

namespace CommanderLayer.Game
{
    /// <summary>
    /// Air-tasking intent: the zones idle player aircraft should ingress to (set from Air-domain orders).
    /// Read by the AIPilotCombatModes.NoTarget postfix. This is deliberately NOT a game Objective — the
    /// decompile shows idle ground vehicles (GroundVehicle) and ships (carrier/landing-craft AI) also seek
    /// the nearest faction objective/MissionPosition, so an objective would re-create the v1 stampede.
    /// Steering the aircraft pilot state directly keeps it aircraft-only. Disabled unless Enabled (config).
    /// </summary>
    public static class AircraftIntent
    {
        /// <summary>Feature flag (Plugin "EnableAircraftTasking" config). Off by default — needs in-game tuning.</summary>
        public static bool Enabled;

        private static readonly List<Vec3> _zones = new List<Vec3>();

        public static void SetZones(IEnumerable<Vec3> centers)
        {
            _zones.Clear();
            if (centers != null) _zones.AddRange(centers);
        }

        public static void Clear() => _zones.Clear();

        /// <summary>Nearest air-intent zone to a position, if tasking is enabled and any zone is active.</summary>
        public static bool TryNearest(Vec3 from, out Vec3 center)
        {
            center = default;
            if (!Enabled || _zones.Count == 0) return false;
            float best = float.MaxValue;
            bool found = false;
            foreach (var z in _zones)
            {
                float d = z.HorizontalDistanceTo(from);
                if (d < best) { best = d; center = z; found = true; }
            }
            return found;
        }
    }
}
