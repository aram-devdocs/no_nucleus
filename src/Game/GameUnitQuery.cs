using System.Collections.Generic;
using CommanderLayer.Core.Model;
using CommanderLayer.Core.Ports;

namespace CommanderLayer.Game
{
    /// <summary>IUnitQuery over UnitRegistry.allUnits, filtered to the local faction.</summary>
    public sealed class GameUnitQuery : IUnitQuery
    {
        private static readonly IReadOnlyList<UnitInfo> EmptyList = new List<UnitInfo>();

        public IReadOnlyList<UnitInfo> GetFriendlyUnits()
        {
            if (!GameManager.GetLocalHQ(out var hq) || hq == null)
            {
                return EmptyList;
            }

            var result = new List<UnitInfo>();
            foreach (var u in UnitRegistry.allUnits)
            {
                if (u == null || u.NetworkHQ != hq)
                {
                    continue;
                }

                string typeName = (u.definition != null && !string.IsNullOrEmpty(u.definition.unitName))
                    ? u.definition.unitName
                    : u.GetType().Name;

                result.Add(new UnitInfo(
                    id: u.GetInstanceID().ToString(),
                    name: string.IsNullOrEmpty(u.unitName) ? typeName : u.unitName,
                    typeName: typeName,
                    position: GameConvert.ToVec3(u.transform.GlobalPosition()),
                    commandable: u is ICommandable,
                    disabled: u.disabled));
            }
            return result;
        }
    }
}
