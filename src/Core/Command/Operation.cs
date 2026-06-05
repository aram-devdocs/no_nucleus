using System.Collections.Generic;
using CommanderLayer.Core.Model;

namespace CommanderLayer.Core.Command
{
    public enum OperationStatus { Planning, Active, Complete, Failed }

    /// <summary>
    /// A commander operation: one <see cref="Objective"/>, the force assigned to it (squad ids), an autonomy
    /// level, and a battle-plan phase. Execution is delegated to the existing per-unit pipeline via a linked
    /// <see cref="CommanderOrder"/> id (the brain issues/updates that order). Pure state; the phase engine
    /// (gating SEAD→strike→assault) is layered on in P2.
    /// </summary>
    public sealed class Operation
    {
        public string Id { get; }
        public Objective Objective { get; }
        public List<string> SquadIds { get; }
        public AutonomyLevel Autonomy { get; set; }
        public OperationStatus Status { get; set; }
        public OrderPhase Phase { get; set; }
        /// <summary>Combined-arms phase cursor (advances via PhaseGates each tick).</summary>
        public CombatPhase CombatPhase { get; set; } = CombatPhase.Recon;
        /// <summary>Threat at the objective when the operation opened — the baseline for the "softened" gate.</summary>
        public ThreatPicture InitialThreat { get; set; }
        /// <summary>The CommanderOrder this operation drives through the AssignmentManager (null until issued).</summary>
        public string OrderId { get; set; }

        public Operation(string id, Objective objective, IEnumerable<string> squadIds = null)
        {
            Id = id;
            Objective = objective;
            SquadIds = squadIds != null ? new List<string>(squadIds) : new List<string>();
            Autonomy = AutonomyLevel.Auto;
            Status = OperationStatus.Planning;
            Phase = OrderPhase.Forming;
        }

        public bool IsTerminal => Status == OperationStatus.Complete || Status == OperationStatus.Failed;
    }
}
