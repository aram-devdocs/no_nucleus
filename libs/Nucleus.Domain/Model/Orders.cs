namespace Nucleus.Core.Model
{
    /// <summary>What a single unit is told to do in a <see cref="UnitTask"/>: move to a point, hold, or attack a target.</summary>
    public enum TaskVerb
    {
        MoveTo,
        Hold,
        AttackTarget
    }

    /// <summary>One concrete instruction for one unit — the brain's executable output — with a phase index for sequencing.</summary>
    public sealed class UnitTask
    {
        public string UnitId { get; }
        public TaskVerb Verb { get; }
        public Vec3 Position { get; }
        public string TargetId { get; }
        public int Phase { get; }

        public UnitTask(string unitId, TaskVerb verb, Vec3 position, string targetId = null, int phase = 0)
        {
            UnitId = unitId;
            Verb = verb;
            Position = position;
            TargetId = targetId;
            Phase = phase;
        }
    }

    /// <summary>An operation's battle-plan phase. Persisted in the campaign save (Operation.Phase).</summary>
    public enum OrderPhase { Forming, Advancing, Engaging, Suppressing, Holding, AirTasking, Queued, Complete, Failed }

    /// <summary>Commander tunables. Pure data.</summary>
    public sealed class CommanderConfig
    {
        /// <summary>Seconds between management ticks — the brain runs on this throttle, not per frame.</summary>
        public float ManagementIntervalSeconds { get; set; } = 3f;
    }
}
