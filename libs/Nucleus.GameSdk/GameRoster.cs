using System.Collections.Generic;
using CommanderLayer.Core.Model;
using CommanderLayer.Core.Roles;

namespace CommanderLayer.Game
{
    /// <summary>Builds the classified friendly roster (the planner's "who's available") from UnitRegistry.</summary>
    public sealed class GameRoster
    {
        private static readonly IReadOnlyList<UnitView> Empty = new List<UnitView>();

        public IReadOnlyList<UnitView> BuildRoster()
        {
            if (!GameManager.GetLocalHQ(out var hq) || hq == null)
            {
                return Empty;
            }

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
                    u.GetInstanceID().ToString(),
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
