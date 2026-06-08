using System.Collections.Generic;
using Nucleus.Core.Model;

namespace Nucleus.Core.Command
{
    /// <summary>Lifecycle of an order: actively being pursued, achieved, or abandoned (force lost).</summary>
    public enum OrderStatus { Active, Complete, Failed }

    /// <summary>
    /// A parent goal the commander pursues — e.g. "capture this airbase" — decomposed by
    /// <see cref="OrderPlanner"/> into a dependency-sequenced tree of child <see cref="Objective"/>s (recon →
    /// control airspace → suppress air defence → naval strike → the goal). The Order is the visible, seizable
    /// node; its children drive the existing phased <see cref="Operation"/> engine unchanged. Pure state.
    /// </summary>
    public sealed class Order
    {
        public string Id { get; }
        public ObjectiveKind GoalKind { get; }
        public Vec3 Position { get; }
        public float Priority { get; set; }
        public ObjectiveSource Source { get; }
        /// <summary><see cref="AutonomyLevel.Manual"/> = the player has taken this order over; the brain yields it.</summary>
        public AutonomyLevel Autonomy { get; set; } = AutonomyLevel.Auto;
        public OrderStatus Status { get; set; } = OrderStatus.Active;
        /// <summary>Every child objective id (prerequisites + the goal), in creation order.</summary>
        public List<string> ChildObjectiveIds { get; } = new List<string>();
        /// <summary>The child that IS the goal — the order completes when this one is achieved.</summary>
        public string GoalObjectiveId { get; set; }
        /// <summary>Game time (seconds) the order was created — the escalation clock so a goal can't be gated
        /// forever by a prerequisite that never resolves.</summary>
        public float CreatedTime { get; set; }
        /// <summary>Game time (seconds) the order went terminal — drives the deterministic fade-then-prune grace
        /// window. <see cref="float.NaN"/> while active.</summary>
        public float TerminalTime { get; set; } = float.NaN;

        public Order(string id, ObjectiveKind goalKind, Vec3 position, float priority, ObjectiveSource source)
        {
            Id = id;
            GoalKind = goalKind;
            Position = position;
            Priority = priority;
            Source = source;
        }

        public bool IsTerminal => Status == OrderStatus.Complete || Status == OrderStatus.Failed;
    }
}
