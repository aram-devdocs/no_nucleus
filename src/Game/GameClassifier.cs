using CommanderLayer.Core.Model;
using Gen = CommanderLayer.Core.Generated; // alias: plugin also sees the game's global enums

namespace CommanderLayer.Game
{
    /// <summary>
    /// Extracts Unity-free UnitDescriptors from real game units. Maps the game's own subtype enums to the
    /// generated mirror by value (the drift contract test guarantees the values match), so classification
    /// stays in pure, tested Core.
    /// </summary>
    internal static class GameClassifier
    {
        public static UnitClass ClassOf(Unit u)
        {
            if (u is Aircraft) return UnitClass.Aircraft;
            if (u is Ship) return UnitClass.Ship;
            if (u is GroundVehicle) return UnitClass.GroundVehicle;
            if (u is Missile) return UnitClass.Missile;
            if (u is Building) return UnitClass.Building;
            if (u is PilotDismounted) return UnitClass.Infantry;
            return UnitClass.Other;
        }

        public static UnitDescriptor Describe(Unit u)
        {
            var def = u.definition;
            RoleIdentity role = def != null ? def.roleIdentity : default;
            UnitClass cls = ClassOf(u);

            Gen.VehicleType? veh = null;
            Gen.ShipType? ship = null;
            Gen.BuildingType? bld = null;
            if (def is VehicleDefinition vd) veh = (Gen.VehicleType)(int)vd.vehicleType;
            else if (def is ShipDefinition sd) ship = (Gen.ShipType)(int)sd.shipType;
            else if (def is BuildingDefinition bd) bld = (Gen.BuildingType)(int)bd.buildingType;

            return new UnitDescriptor(
                cls,
                role.antiSurface, role.antiAir, role.antiRadar, role.antiMissile,
                hasRadar: false, hasTroops: false, hasCargo: false, // weapon-mount flags: P4 refinement
                captureStrength: u.CaptureStrength,
                armorTier: def != null ? (int)def.armorTier : 0,
                commandable: u is ICommandable,
                vehicle: veh, ship: ship, building: bld);
        }
    }
}
