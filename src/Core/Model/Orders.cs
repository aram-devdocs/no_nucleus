using System.Collections.Generic;

namespace CommanderLayer.Core.Model
{
    /// <summary>Commander order families (P1 ships Attack + Defend; more added in later phases).</summary>
    public enum OrderKind
    {
        Attack,   // move-attack a point, or attack a specific target (TargetId set)
        Defend,   // hold/garrison + air-defense an area
        Resupply, // (P2)
        Capture,  // (P2)
        Build     // (P2) commission only
    }

    public sealed class CommanderOrder
    {
        public string Id { get; }
        public OrderKind Kind { get; }
        public Vec3 Position { get; }
        /// <summary>The pull radius around the point: units within it are eligible. 0 = use config default.</summary>
        public float Radius { get; }
        /// <summary>Which domains (air/land/sea) the player allows this order to commit.</summary>
        public DomainSet Domains { get; }
        /// <summary>For Attack on a specific enemy; null = area attack.</summary>
        public string TargetId { get; }

        public CommanderOrder(string id, OrderKind kind, Vec3 position, float radius,
            DomainSet domains = DomainSet.All, string targetId = null)
        {
            Id = id;
            Kind = kind;
            Position = position;
            Radius = radius;
            Domains = domains;
            TargetId = targetId;
        }
    }

    public enum TaskVerb
    {
        MoveTo,
        Hold,
        AttackTarget
    }

    /// <summary>One concrete instruction for one unit, with a phase index for sequencing.</summary>
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

    public sealed class TaskPlan
    {
        public string OrderId { get; }
        public IReadOnlyList<UnitTask> Tasks { get; }

        public TaskPlan(string orderId, IReadOnlyList<UnitTask> tasks)
        {
            OrderId = orderId;
            Tasks = tasks;
        }

        public bool IsEmpty => Tasks.Count == 0;
    }

    public enum OrderStatus { Planning, Active, Complete, Failed }

    /// <summary>Live state of an order managed over time.</summary>
    public sealed class OrderState
    {
        public CommanderOrder Order { get; }
        public OrderStatus Status { get; set; }
        public List<string> AssignedUnitIds { get; }
        /// <summary>Units that have already been told to hold (Defend), so we don't re-issue.</summary>
        public HashSet<string> Held { get; }
        public string Summary { get; set; }

        public OrderState(CommanderOrder order)
        {
            Order = order;
            Status = OrderStatus.Planning;
            AssignedUnitIds = new List<string>();
            Held = new HashSet<string>();
            Summary = string.Empty;
        }
    }

    /// <summary>Tactics tunables (F1 + in-panel). Pure data.</summary>
    public sealed class CommanderConfig
    {
        public int MaxUnitsPerOrder { get; set; } = 6;
        /// <summary>Radius (m) around the order point within which units are considered for selection.</summary>
        public float SelectionRadius { get; set; } = 6000f;
        /// <summary>Distance (m) at which a unit counts as arrived.</summary>
        public float ArriveRadius { get; set; } = 250f;
        /// <summary>Radius (m) used to read enemies near the order point.</summary>
        public float ThreatRadius { get; set; } = 3000f;
        public bool AutoReassign { get; set; } = true;
        public float ManagementIntervalSeconds { get; set; } = 3f;
    }
}
