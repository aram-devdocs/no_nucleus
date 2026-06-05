using System.Collections.Generic;

namespace CommanderLayer.Core.Command
{
    /// <summary>
    /// The persistent state of one faction's commander: its objectives, operations, squads, doctrine and
    /// top-level autonomy. Mutated each tick by <see cref="CommanderBrain.Tick"/>. Pure (engine-free) so the
    /// whole brain is a function of (snapshot, state) — the seam that lets AI commanders + multiplayer reuse
    /// it unchanged.
    /// </summary>
    public sealed class CommanderState
    {
        public List<Objective> Objectives { get; } = new List<Objective>();
        public List<Operation> Operations { get; } = new List<Operation>();
        public SquadRoster Squads { get; }
        public Doctrine Doctrine { get; }
        public BrainConfig BrainConfig { get; }
        public AutonomyLevel Autonomy { get; set; } = AutonomyLevel.Auto;
        /// <summary>The battle feed — the brain appends events (op started/phase changed/completed) here.</summary>
        public BattleLog Log { get; } = new BattleLog();
        /// <summary>Last objective each unit was tasked toward — so the brain only re-issues on change (no spam).</summary>
        public Dictionary<string, string> LastObjectiveByUnit { get; } = new Dictionary<string, string>();

        private int _opId;

        public CommanderState(SquadConfig squadCfg = null, Doctrine doctrine = null, BrainConfig brainCfg = null)
        {
            Squads = new SquadRoster(squadCfg ?? new SquadConfig());
            Doctrine = doctrine ?? new Doctrine();
            BrainConfig = brainCfg ?? new BrainConfig();
        }

        public string NextOperationId() => "op-" + (++_opId);

        public Operation OperationFor(string objectiveId) =>
            Operations.Find(op => op.Objective.Id == objectiveId && !op.IsTerminal);
    }
}
