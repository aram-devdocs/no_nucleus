namespace CommanderLayer.Core.Model
{
    /// <summary>
    /// The single source of truth the UI renders and the controller mutates. Immutable snapshots:
    /// the controller produces a new instance on each change so the UI can diff/re-render cleanly.
    /// </summary>
    public sealed class CommanderState
    {
        public bool HasLocalFaction { get; }
        public FactionInfo Faction { get; }

        /// <summary>The active objective, or null if none is placed.</summary>
        public ObjectiveModel Objective { get; }

        /// <summary>True while the player has armed placement and the next map click drops the objective.</summary>
        public bool PlacementArmed { get; }

        public AssignmentSnapshot Assignments { get; }

        public string StatusLine { get; }

        public CommanderState(
            bool hasLocalFaction,
            FactionInfo faction,
            ObjectiveModel objective,
            bool placementArmed,
            AssignmentSnapshot assignments,
            string statusLine)
        {
            HasLocalFaction = hasLocalFaction;
            Faction = faction;
            Objective = objective;
            PlacementArmed = placementArmed;
            Assignments = assignments ?? AssignmentSnapshot.Empty;
            StatusLine = statusLine ?? string.Empty;
        }

        public static CommanderState NoFaction =>
            new CommanderState(false, null, null, false, AssignmentSnapshot.Empty, "No faction joined.");
    }
}
