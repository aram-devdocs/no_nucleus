namespace CommanderLayer.Core.Model
{
    /// <summary>
    /// A snapshot of one friendly unit, produced by IUnitQuery. A plain DTO so Core logic and tests
    /// never touch the engine's Unit type.
    /// </summary>
    public sealed class UnitInfo
    {
        public string Id { get; }
        public string Name { get; }
        public string TypeName { get; }
        public Vec3 Position { get; }

        /// <summary>True if the unit can receive a direct move command (Ship/GroundVehicle/Missile).</summary>
        public bool Commandable { get; }

        public bool Disabled { get; }

        public UnitInfo(string id, string name, string typeName, Vec3 position, bool commandable, bool disabled)
        {
            Id = id;
            Name = name;
            TypeName = typeName;
            Position = position;
            Commandable = commandable;
            Disabled = disabled;
        }
    }
}
