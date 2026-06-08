using System.Collections.Generic;
using Nucleus.Core.Model;

namespace Nucleus.Sim
{
    /// <summary>Battlefield archetypes + scenarios, built from plain UnitCapability (no game classification
    /// needed). Stats are coarse but exercise the brain's combined-arms + targeting logic.</summary>
    public static class Scenarios
    {
        public static SimUnit FriendlyArmor(string id, float x, float z) => new SimUnit
        {
            Id = id, X = x, Z = z, Class = UnitClass.GroundVehicle, Speed = 300f, ArmorTier = 3,
            AntiSurface = 1f, AntiAir = 0f,
            Cap = new UnitCapability(Role.Armor, canEngageGround: true, canEngageAir: false, canCapture: true, isSupply: false, isAirDefense: false),
        };

        public static SimUnit FriendlyFighter(string id, float x, float z) => new SimUnit
        {
            Id = id, X = x, Z = z, Class = UnitClass.Aircraft, Speed = 2000f, ArmorTier = 1,
            AntiSurface = 0.3f, AntiAir = 1f,
            Cap = new UnitCapability(Role.Fighter, canEngageGround: false, canEngageAir: true, canCapture: false, isSupply: false, isAirDefense: false),
        };

        public static SimUnit EnemyArmor(string id, float x, float z) => new SimUnit
        {
            Id = id, X = x, Z = z, Class = UnitClass.GroundVehicle, Speed = 0f, ArmorTier = 3, StrategicPriority = 2f,
            AntiSurface = 1f, AntiAir = 0f,
            Cap = new UnitCapability(Role.Armor, true, false, true, false, false),
        };

        public static SimUnit EnemySam(string id, float x, float z) => new SimUnit
        {
            Id = id, X = x, Z = z, Class = UnitClass.GroundVehicle, Speed = 0f, ArmorTier = 2, StrategicPriority = 3f,
            AntiSurface = 0f, AntiAir = 1f,
            Cap = new UnitCapability(Role.GroundAirDefense, false, true, false, false, isAirDefense: true),
        };

        public static SimUnit EnemyAircraft(string id, float x, float z) => new SimUnit
        {
            Id = id, X = x, Z = z, Class = UnitClass.Aircraft, Speed = 1800f, ArmorTier = 1, StrategicPriority = 2f,
            AntiSurface = 0.3f, AntiAir = 1f,
            Cap = new UnitCapability(Role.Fighter, false, true, false, false, false),
        };

        /// <summary>A combined-arms engagement: a friendly armor+air force near origin vs an enemy ground
        /// cluster (armor + SAM) and an enemy aircraft, all within sensor range so the brain detects them.</summary>
        public static (List<SimUnit> friendly, List<SimUnit> enemy) CombinedArms()
        {
            var friendly = new List<SimUnit>
            {
                FriendlyArmor("f-armor-1", 0f, 0f),
                FriendlyArmor("f-armor-2", 200f, 0f),
                FriendlyArmor("f-armor-3", 400f, 0f),
                FriendlyArmor("f-armor-4", 0f, 200f),
                FriendlyFighter("f-fighter-1", 0f, 400f),
                FriendlyFighter("f-fighter-2", 200f, 400f),
            };
            var enemy = new List<SimUnit>
            {
                EnemyArmor("e-armor-1", 4000f, 0f),
                EnemyArmor("e-armor-2", 4200f, 0f),
                EnemyArmor("e-armor-3", 4000f, 200f),
                EnemySam("e-sam-1", 4100f, 100f),
                EnemyAircraft("e-air-1", 3000f, 2000f),
            };
            return (friendly, enemy);
        }

        /// <summary>Two mobile armored forces facing each other ~6 km apart — for the dual-faction sim where
        /// BOTH sides run a brain. Pure armor (no aircraft): aircraft are steered by intent zones rather than
        /// per-unit tasks, which the sim doesn't model, so a ground clash cleanly exercises both brains fighting.</summary>
        public static (List<SimUnit> a, List<SimUnit> b) DualForces()
        {
            List<SimUnit> Force(string p, float ox, float oz) => new List<SimUnit>
            {
                FriendlyArmor($"{p}-armor-1", ox, oz),
                FriendlyArmor($"{p}-armor-2", ox + 200f, oz),
                FriendlyArmor($"{p}-armor-3", ox, oz + 200f),
                FriendlyArmor($"{p}-armor-4", ox + 200f, oz + 200f),
            };
            return (Force("a", 0f, 0f), Force("b", 6000f, 0f));
        }
    }
}
