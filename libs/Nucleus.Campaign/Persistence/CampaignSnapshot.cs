using System.Collections.Generic;
using Nucleus.Core.Command;
using Nucleus.Core.Model;

namespace Nucleus.Core.Persistence
{
    /// <summary>
    /// A point-in-time, engine-free snapshot of one faction's resumable campaign state — everything the
    /// brain needs to continue a war exactly. Reuses the pure domain types (<see cref="Objective"/>/
    /// <see cref="Squad"/>/<see cref="Operation"/>) directly; <see cref="CampaignState"/> maps to/from a live
    /// <c>CommanderState</c> and <see cref="CampaignSave"/> serializes it. Transient per-tick state
    /// (fog-of-war intel, proposals, production needs, the battle-log feed) is deliberately NOT captured — it
    /// is re-derived from the live game on the next tick.
    /// </summary>
    public sealed class CampaignSnapshot
    {
        public const int CurrentVersion = 2; // v2: two toggles replace the autonomy enum; + ObjectiveIdSeed

        public int Version = CurrentVersion;

        // Top-level commander state.
        public bool AiCreatesObjectives = true;
        public bool AiAutoFill = true;
        public Vec3 HomeBase;
        public int OperationIdSeed;   // last issued operation-id counter
        public int SquadBatchSeed;    // auto-form batch counter
        public int ObjectiveIdSeed;   // last issued auto-objective-id counter

        // Tunables (Doctrine / BrainConfig / SquadConfig).
        public float RiskTolerance = 0.5f;
        public float ForceRatio = 1.5f;
        public float ClusterRadius = 3000f;
        public float CoverageRadius = 4000f;
        public int MaxSquadsPerOperation = 2;
        public float FormRadius = 4000f;
        public int MaxSquadSize = 5;
        public float DepletedFraction = 0.5f;

        // Collections (domain instances; rebuilt fresh on restore).
        public readonly List<Objective> Objectives = new List<Objective>();
        public readonly List<Squad> Squads = new List<Squad>();
        public readonly List<Operation> Operations = new List<Operation>();
        public readonly List<string> ConfirmedObjectives = new List<string>();
        public readonly List<KeyValuePair<string, string>> LastObjectiveByUnit = new List<KeyValuePair<string, string>>();
    }
}
