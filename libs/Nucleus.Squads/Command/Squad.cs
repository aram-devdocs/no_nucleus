using System.Collections.Generic;

namespace CommanderLayer.Core.Command
{
    public enum SquadOrigin { Auto, Player }

    public enum SquadStatus { Forming, Ready, Engaged, Depleted, Reserve }

    /// <summary>
    /// A named group of units with an intended role, the unit of force a commander assigns to operations.
    /// Auto-formed from loose units (<see cref="SquadFormer"/>) or player-created. Pure state — reconciled
    /// against the live roster each tick by <see cref="SquadRoster"/>. No Unity types.
    /// </summary>
    public sealed class Squad
    {
        public string Id { get; }
        public string Name { get; set; }
        public SquadOrigin Origin { get; }
        public RoleFamily Family { get; }
        public List<string> MemberUnitIds { get; }
        public string AssignedOperationId { get; set; }
        public Composition TargetComposition { get; set; }
        public SquadStatus Status { get; set; }
        public AutonomyLevel Autonomy { get; set; }

        public Squad(string id, string name, RoleFamily family, SquadOrigin origin, IEnumerable<string> members = null)
        {
            Id = id;
            Name = name;
            Family = family;
            Origin = origin;
            MemberUnitIds = members != null ? new List<string>(members) : new List<string>();
            Status = SquadStatus.Forming;
            Autonomy = AutonomyLevel.Auto;
        }

        public int Strength => MemberUnitIds.Count;
        public bool IsEmpty => MemberUnitIds.Count == 0;
    }
}
