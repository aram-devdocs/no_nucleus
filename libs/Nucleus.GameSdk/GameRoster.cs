using System.Collections.Generic;
using Nucleus.Core.Model;
using Nucleus.Core.Roles;

namespace Nucleus.Game
{
    /// <summary>Builds the classified friendly roster (the planner's "who's available") from UnitRegistry.</summary>
    public sealed class GameRoster
    {
        private static readonly IReadOnlyList<UnitView> Empty = new List<UnitView>();

        // Memoize the per-unit id STRING by instance id: GetInstanceID() never changes for a unit, but
        // .ToString() allocates a fresh heap string on every roster build (~400 units, every 1.5–3s). The
        // classification (Describe/Classify) is NOT cached — it reads the live, mutable CaptureStrength.
        private readonly Dictionary<int, string> _idStr = new Dictionary<int, string>();

        private string IdString(int instanceId)
        {
            if (!_idStr.TryGetValue(instanceId, out var s)) { s = instanceId.ToString(); _idStr[instanceId] = s; }
            return s;
        }

        public IReadOnlyList<UnitView> BuildRoster()
        {
            return GameManager.GetLocalHQ(out var hq) && hq != null ? BuildRosterFor(hq) : Empty;
        }

        /// <summary>Build the classified roster for a SPECIFIC faction HQ — used to drive an AI commander for the
        /// non-local (enemy) faction in-mission (we are the offline host, so we can task any faction's units).</summary>
        public IReadOnlyList<UnitView> BuildRosterFor(FactionHQ hq)
        {
            if (hq == null) return Empty;
            var list = new List<UnitView>();
            foreach (var u in UnitRegistry.allUnits)
            {
                if (u == null || u.disabled || u.NetworkHQ != hq)
                {
                    continue;
                }
                var d = GameClassifier.Describe(u);
                var cap = RoleClassifier.Classify(d);
                var pos = GameConvert.ToVec3(u.transform.GlobalPosition());
                list.Add(new UnitView(
                    IdString(u.GetInstanceID()),
                    DisplayName(u),
                    pos,
                    d.Class,
                    u.disabled,
                    d.Commandable,
                    cap,
                    d.AntiSurface,
                    d.AntiAir,
                    d.ArmorTier));
            }
            return list;
        }

        private static string DisplayName(Unit u)
        {
            if (!string.IsNullOrEmpty(u.unitName)) return u.unitName;
            return u.definition != null && !string.IsNullOrEmpty(u.definition.unitName)
                ? u.definition.unitName
                : u.GetType().Name;
        }
    }
}
