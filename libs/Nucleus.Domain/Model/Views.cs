using System.Collections.Generic;

namespace Nucleus.Core.Model
{
    /// <summary>A classified friendly unit snapshot the planner selects from.</summary>
    public sealed class UnitView
    {
        public string Id { get; }
        public string Name { get; }
        public Vec3 Position { get; }
        public UnitClass Class { get; }
        public bool Disabled { get; }
        public bool Commandable { get; }
        public UnitCapability Cap { get; }
        public float AntiSurface { get; }
        public float AntiAir { get; }
        public int ArmorTier { get; }

        public UnitView(string id, string name, Vec3 position, UnitClass cls, bool disabled, bool commandable,
            UnitCapability cap, float antiSurface, float antiAir, int armorTier)
        {
            Id = id;
            Name = name;
            Position = position;
            Class = cls;
            Disabled = disabled;
            Commandable = commandable;
            Cap = cap;
            AntiSurface = antiSurface;
            AntiAir = antiAir;
            ArmorTier = armorTier;
        }

        public Role Role => Cap.Role;
    }

    /// <summary>A known enemy (fog-of-war: only what the faction has detected), classified.</summary>
    public sealed class EnemyView
    {
        public string Id { get; }
        public Vec3 Position { get; }
        public UnitClass Class { get; }
        public UnitCapability Cap { get; }
        public bool Accurate { get; }
        public float StrategicPriority { get; }
        public int ArmorTier { get; }

        public EnemyView(string id, Vec3 position, UnitClass cls, UnitCapability cap, bool accurate,
            float strategicPriority, int armorTier)
        {
            Id = id;
            Position = position;
            Class = cls;
            Cap = cap;
            Accurate = accurate;
            StrategicPriority = strategicPriority;
            ArmorTier = armorTier;
        }
    }

    /// <summary>What the commander knows is near an order point — drives tactic selection.</summary>
    public sealed class ThreatPicture
    {
        public static readonly ThreatPicture Empty = new ThreatPicture(new List<EnemyView>());

        public IReadOnlyList<EnemyView> Enemies { get; }
        public bool HasAirDefense { get; }
        public bool HasRadar { get; }
        public bool HasArmor { get; }
        public bool HasBuildings { get; }
        public bool HasAir { get; }
        public bool HasNaval { get; }

        // Numeric counts the combined-arms phase gates threshold against.
        public int AirDefenseCount { get; }
        public int ArmorCount { get; }
        public int AirCount { get; }
        public int RadarCount { get; }
        public int NavalCount { get; }

        public ThreatPicture(IReadOnlyList<EnemyView> enemies)
        {
            Enemies = enemies ?? new List<EnemyView>();   // coalesce (matches WorldSnapshot) — no bare NRE
            foreach (var e in Enemies)
            {
                if (e.Cap.IsAirDefense) { HasAirDefense = true; AirDefenseCount++; }
                if (e.Cap.Role == Role.GroundRadar || e.Cap.Role == Role.AwacsEw) { HasRadar = true; RadarCount++; }
                if (e.Cap.Role == Role.Armor || e.Cap.Role == Role.Ifv) { HasArmor = true; ArmorCount++; }
                if (e.Cap.Role == Role.Carrier || e.Cap.Role == Role.CombatShip || e.Cap.Role == Role.TransportShip)
                    { HasNaval = true; NavalCount++; }
                if (e.Class == UnitClass.Building) HasBuildings = true;
                if (e.Class == UnitClass.Aircraft) { HasAir = true; AirCount++; }
            }
        }

        public int Count => Enemies.Count;
    }
}
