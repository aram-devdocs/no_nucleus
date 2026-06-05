using System.Collections.Generic;
using CommanderLayer.Core.Model;
using CommanderLayer.Core.Roles;

namespace CommanderLayer.Game
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
            if (!GameManager.GetLocalHQ(out var hq) || hq == null)
            {
                return Empty;
            }

            var list = new List<EnemyView>();
            foreach (var kv in hq.trackingDatabase)
            {
                var info = kv.Value;
                if (info == null) continue;
                var pos = GameConvert.ToVec3(info.GetPosition());
                if (pos.HorizontalDistanceTo(center) > radius) continue;

                UnitClass cls = UnitClass.Other;
                UnitCapability cap = default;
                int armor = 0;
                bool accurate = false;
                if (info.TryGetUnit(out var u) && u != null)
                {
                    if (u.NetworkHQ == hq) continue; // guard: never treat own units as enemies
                    var d = GameClassifier.Describe(u);
                    cap = RoleClassifier.Classify(d);
                    cls = d.Class;
                    armor = d.ArmorTier;
                    accurate = true;
                }
                list.Add(new EnemyView(kv.Key.ToString(), pos, cls, cap, accurate, info.GetStrategicPriority(), armor));
            }
            return list;
        }
    }
}
