using System.Collections.Generic;
using Nucleus.Core.Model;

namespace Nucleus.Core.Command
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
        /// <summary>The mod is always on. Two toggles replace the old Off/Manual/Assisted/Auto ladder:</summary>
        /// <summary>Who creates objectives — the AI (true) or only the human (false).</summary>
        public bool AiCreatesObjectives { get; set; } = true;
        /// <summary>Whether the AI fills objectives: forms squads, assigns them per phase, and recruits.
        /// Off = the human assigns squads / recruits. Default on.</summary>
        public bool AiAutoFill { get; set; } = true;
        /// <summary>Home base / HQ position — used to weight target proximity in prioritization.</summary>
        public Vec3 HomeBase { get; set; }
        /// <summary>The battle feed — the brain appends events (op started/phase changed/completed) here.</summary>
        public BattleLog Log { get; } = new BattleLog();
        /// <summary>Force compositions the brain couldn't field — the Game layer turns these into convoy buys.</summary>
        public List<Composition> ProductionNeeds { get; } = new List<Composition>();
        /// <summary>Last objective each unit was tasked toward — so the brain only re-issues on change (no spam).</summary>
        public Dictionary<string, string> LastObjectiveByUnit { get; } = new Dictionary<string, string>();
        /// <summary>Objective ids carried for persistence compatibility (the old Assisted proposal flow is gone;
        /// the two-toggle model replaced it). Retained so the save format is unchanged.</summary>
        public HashSet<string> ConfirmedObjectives { get; } = new HashSet<string>();

        private readonly List<string> _purgeScratch = new List<string>();
        private int _opId;
        private int _objId;

        /// <summary>Forget tasking memory for units not tasked this tick (reused buffer; can't remove mid-enumeration).</summary>
        public void PurgeUntaskedMemory(ICollection<string> tasked)
        {
            _purgeScratch.Clear();
            foreach (var k in LastObjectiveByUnit.Keys)
                if (!tasked.Contains(k)) _purgeScratch.Add(k);
            foreach (var k in _purgeScratch) LastObjectiveByUnit.Remove(k);
        }

        /// <summary>The operation-id counter (last issued). Exposed so persistence can save/restore it,
        /// keeping <see cref="NextOperationId"/> from colliding with restored ids. Same-assembly only.</summary>
        internal int OperationIdSeed { get => _opId; set => _opId = value; }

        /// <summary>The auto-objective-id counter (last issued). Persisted so monotonic ids survive save/resume
        /// and never collide across ticks.</summary>
        internal int ObjectiveIdSeed { get => _objId; set => _objId = value; }

        /// <summary>A unique, monotonic auto-objective id (never reused across ticks).</summary>
        public string NextObjectiveId() => "auto-obj-" + (++_objId);

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
