using System.Collections.Generic;

namespace CommanderLayer.Core.Model
{
    /// <summary>
    /// What WOULD be assigned if an order were placed at a point (drives the live hover UI): the units the
    /// planner would pick, plus the threat known there. Pure, computed without mutating any state.
    /// </summary>
    public sealed class AssignmentPreview
    {
        public static readonly AssignmentPreview None =
            new AssignmentPreview(new List<UnitView>(), ThreatPicture.Empty);

        public IReadOnlyList<UnitView> Assignable { get; }
        public ThreatPicture Threat { get; }

        public AssignmentPreview(IReadOnlyList<UnitView> assignable, ThreatPicture threat)
        {
            Assignable = assignable;
            Threat = threat;
        }

        public bool CanPlace => Assignable.Count > 0;
        public int Count => Assignable.Count;
    }
}
