using System.Collections.Generic;
using Nucleus.Core.Model;
using Nucleus.Core.Roles;

namespace Nucleus.Game
{
    /// <summary>
    /// Fog-of-war intel: the enemies the local faction has DETECTED near a point, from
    /// FactionHQ.trackingDatabase (last-known positions). Never reads ground truth for enemies.
    /// </summary>
    public sealed class GameIntel
    {
        private static readonly IReadOnlyList<EnemyView> Empty = new List<EnemyView>();

        public IReadOnlyList<EnemyView> KnownEnemiesNear(Vec3 center, float radius)
        {
            return GameManager.GetLocalHQ(out var hq) && hq != null ? KnownEnemiesNearFor(hq, center, radius) : Empty;
        }

        /// <summary>The enemies a SPECIFIC faction HQ has detected — used to drive an AI commander for the
        /// non-local (enemy) faction in-mission. Reads that HQ's own fog-of-war tracking, never ground truth.</summary>
        public IReadOnlyList<EnemyView> KnownEnemiesNearFor(FactionHQ hq, Vec3 center, float radius)
        {
            if (hq == null) return Empty;
            var list = new List<EnemyView>();
            foreach (var kv in hq.trackingDatabase)
            {
                var info = kv.Value;
                if (info == null) continue;
                var pos = GameConvert.ToVec3(info.GetPosition());
                if (pos.HorizontalDistanceTo(center) > radius) continue;

                // Only emit a CONFIRMED enemy: the tracking entry must resolve to a live unit of a DIFFERENT
                // faction. Unresolvable/stale entries (destroyed units, contacts that no longer map to a Unit)
                // and own/neutral units are dropped — otherwise they become phantom enemies at stale positions,
                // and the brain drops DestroyTarget objectives on our own bases / empty map (bug: phantom ops).
                if (!info.TryGetUnit(out var u) || u == null) continue;
                if (u.NetworkHQ == hq) continue;                 // never treat own units as enemies
                if (u.NetworkHQ == null) continue;               // unaligned/neutral — not a war target
                var d = GameClassifier.Describe(u);
                var cap = RoleClassifier.Classify(d);
                list.Add(new EnemyView(kv.Key.ToString(), pos, d.Class, cap, true, info.GetStrategicPriority(), d.ArmorTier));
            }
            return list;
        }
    }
}
