using System.Collections.Generic;
using Nucleus.Core.Model;

namespace Nucleus.Core.Command
{
    /// <summary>Lifecycle of a commander operation: being planned, actively running, completed, or failed/abandoned.</summary>
    public enum OperationStatus { Planning, Active, Complete, Failed }

    /// <summary>
    /// A commander operation: one <see cref="Objective"/>, the force assigned to it (squad ids), an autonomy
    /// level, and a combat-phase cursor the brain advances each tick (gating recon→air→SEAD→strike→assault) to
    /// emit per-unit <see cref="Nucleus.Core.Model.UnitTask"/>s. Pure state.
    /// </summary>
    public sealed class Operation
    {
        public string Id { get; }
        public Objective Objective { get; }
        public List<string> SquadIds { get; }
        public AutonomyLevel Autonomy { get; set; }
        public OperationStatus Status { get; set; }
        /// <summary>Combined-arms phase cursor (advances via PhaseGates each tick).</summary>
        public CombatPhase CombatPhase { get; set; } = CombatPhase.Recon;
        /// <summary>Threat at the objective when the operation opened — the baseline for the "softened" gate.</summary>
        public ThreatPicture InitialThreat { get; set; }

        public Operation(string id, Objective objective, IEnumerable<string> squadIds = null)
        {
            Id = id;
            Objective = objective;
            SquadIds = squadIds != null ? new List<string>(squadIds) : new List<string>();
            Autonomy = AutonomyLevel.Auto;
            Status = OperationStatus.Planning;
        }

        public bool IsTerminal => Status == OperationStatus.Complete || Status == OperationStatus.Failed;
    }
}
