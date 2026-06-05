using System.Collections.Generic;
using CommanderLayer.Core.Model;

namespace CommanderLayer.Core.Ports
{
    /// <summary>Enumerates the local faction's units. Implemented by the Game layer over UnitRegistry.</summary>
    public interface IUnitQuery
    {
        /// <summary>All non-disabled units belonging to the local faction.</summary>
        IReadOnlyList<UnitInfo> GetFriendlyUnits();
    }
}
