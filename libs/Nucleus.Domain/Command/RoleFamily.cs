using CommanderLayer.Core.Model;

namespace CommanderLayer.Core.Command
{
    /// <summary>Coarse grouping of tactical roles into squad families (how auto-forming buckets units).</summary>
    public enum RoleFamily
    {
        Armor,       // MBT / IFV / UGV — the ground punch
        Artillery,   // indirect fire
        AirDefense,  // ground SAM/AAA + air-defense ships
        Recon,       // ground radar / AWACS-EW
        Supply,      // trucks / transports
        AirCombat,   // fighters / strike / SEAD aircraft
        Naval,       // carriers / combat + transport ships
        Infantry,
        Other        // munitions / buildings / unclassified (not squadable)
    }

    public static class Families
    {
        public static RoleFamily Of(Role role)
        {
            switch (role)
            {
                case Role.Armor:
                case Role.Ifv:
                case Role.Ugv: return RoleFamily.Armor;
                case Role.Artillery: return RoleFamily.Artillery;
                case Role.GroundAirDefense:
                case Role.AirDefenseShip: return RoleFamily.AirDefense;
                case Role.GroundRadar:
                case Role.AwacsEw: return RoleFamily.Recon;
                case Role.Supply:
                case Role.TransportAir: return RoleFamily.Supply;
                case Role.Fighter:
                case Role.StrikeAircraft:
                case Role.Sead: return RoleFamily.AirCombat;
                case Role.Carrier:
                case Role.CombatShip:
                case Role.TransportShip: return RoleFamily.Naval;
                case Role.Infantry: return RoleFamily.Infantry;
                default: return RoleFamily.Other; // CruiseMissile / BuildingMilitary / Other
            }
        }

        /// <summary>Families we actually form squads from (Other = munitions/buildings, never squadable).</summary>
        public static bool IsSquadable(RoleFamily f) => f != RoleFamily.Other;

        /// <summary>
        /// Which squad families actually engage during a given combined-arms phase — so ground holds back
        /// while aircraft/artillery win air superiority, suppress SAMs and soften the target first.
        /// </summary>
        public static System.Collections.Generic.HashSet<RoleFamily> ActiveInPhase(CombatPhase phase)
        {
            switch (phase)
            {
                case CombatPhase.Recon:
                    return new System.Collections.Generic.HashSet<RoleFamily> { RoleFamily.Recon, RoleFamily.AirCombat };
                case CombatPhase.AirSuperiority:
                case CombatPhase.Sead:
                    return new System.Collections.Generic.HashSet<RoleFamily> { RoleFamily.AirCombat };
                case CombatPhase.Strike:
                    return new System.Collections.Generic.HashSet<RoleFamily> { RoleFamily.AirCombat, RoleFamily.Artillery };
                case CombatPhase.Assault:
                case CombatPhase.Capture:
                    return new System.Collections.Generic.HashSet<RoleFamily> { RoleFamily.Armor, RoleFamily.Infantry };
                case CombatPhase.Hold:
                    return new System.Collections.Generic.HashSet<RoleFamily> { RoleFamily.Armor, RoleFamily.AirDefense };
                default:
                    return new System.Collections.Generic.HashSet<RoleFamily>();
            }
        }

        /// <summary>Which squad families are suited to carry out an objective.</summary>
        public static System.Collections.Generic.HashSet<RoleFamily> SuitableFor(ObjectiveKind kind)
        {
            switch (kind)
            {
                case ObjectiveKind.DestroyTarget:
                    return new System.Collections.Generic.HashSet<RoleFamily> { RoleFamily.Armor, RoleFamily.Artillery, RoleFamily.AirCombat };
                case ObjectiveKind.CapturePoint:
                    return new System.Collections.Generic.HashSet<RoleFamily> { RoleFamily.Armor, RoleFamily.Infantry };
                case ObjectiveKind.DefendArea:
                    return new System.Collections.Generic.HashSet<RoleFamily> { RoleFamily.AirDefense, RoleFamily.Armor };
                case ObjectiveKind.ControlAirspace:
                    return new System.Collections.Generic.HashSet<RoleFamily> { RoleFamily.AirCombat };
                case ObjectiveKind.Resupply:
                    return new System.Collections.Generic.HashSet<RoleFamily> { RoleFamily.Supply };
                case ObjectiveKind.Recon:
                    return new System.Collections.Generic.HashSet<RoleFamily> { RoleFamily.Recon, RoleFamily.AirCombat };
                default:
                    return new System.Collections.Generic.HashSet<RoleFamily> { RoleFamily.Armor };
            }
        }
    }
}
