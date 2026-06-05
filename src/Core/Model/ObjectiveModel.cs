namespace CommanderLayer.Core.Model
{
    /// <summary>The kind of objective the player drops. Milestone 1 ships MoveAttack only.</summary>
    public enum ObjectiveKind
    {
        MoveAttack,
        Defend,
        Attack
    }

    /// <summary>
    /// An immutable commander objective: a world position the faction's idle AI units are pulled toward.
    /// This is the Core representation; the Game layer maps it onto the engine's Objective type.
    /// </summary>
    public sealed class ObjectiveModel
    {
        public string Id { get; }
        public ObjectiveKind Kind { get; }
        public Vec3 Position { get; }

        /// <summary>Optional radius in meters (used by Defend; null = point objective).</summary>
        public float? Radius { get; }

        public string Label { get; }

        public ObjectiveModel(string id, ObjectiveKind kind, Vec3 position, float? radius = null, string label = null)
        {
            Id = id;
            Kind = kind;
            Position = position;
            Radius = radius;
            Label = label ?? kind.ToString();
        }
    }
}
