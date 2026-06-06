using System;

namespace CommanderLayer.Core.Model
{
    /// <summary>Battlefield domain a unit operates in.</summary>
    public enum Domain { Land, Sea, Air }

    /// <summary>Which domains an order is allowed to commit (player air/land/sea checkboxes).</summary>
    [Flags]
    public enum DomainSet
    {
        None = 0,
        Land = 1,
        Sea = 2,
        Air = 4,
        All = Land | Sea | Air
    }

    public static class Domains
    {
        /// <summary>Domain a role belongs to, or null for non-commandable "things" (missiles, buildings).</summary>
        public static Domain? Of(Role role)
        {
            switch (role)
            {
                case Role.Fighter:
                case Role.StrikeAircraft:
                case Role.Bomber:
                case Role.Sead:
                case Role.AwacsEw:
                case Role.TransportAir:
                    return Domain.Air;

                case Role.Carrier:
                case Role.AirDefenseShip:
                case Role.CombatShip:
                case Role.TransportShip:
                    return Domain.Sea;

                case Role.Armor:
                case Role.Ifv:
                case Role.Apc:
                case Role.Artillery:
                case Role.GroundAirDefense:
                case Role.GroundRadar:
                case Role.Supply:
                case Role.Ugv:
                case Role.Infantry:
                    return Domain.Land;

                default: // CruiseMissile, BuildingMilitary, Other → not commandable troops
                    return null;
            }
        }

        public static bool InMask(Domain d, DomainSet mask)
        {
            switch (d)
            {
                case Domain.Land: return (mask & DomainSet.Land) != 0;
                case Domain.Sea: return (mask & DomainSet.Sea) != 0;
                case Domain.Air: return (mask & DomainSet.Air) != 0;
                default: return false;
            }
        }
    }
}
