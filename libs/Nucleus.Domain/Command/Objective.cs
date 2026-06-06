using Nucleus.Core.Model;

namespace Nucleus.Core.Command
{
    /// <summary>What the commander is trying to achieve at a place. Player-dropped or auto-generated.</summary>
    public enum ObjectiveKind { CapturePoint, DestroyTarget, DefendArea, ControlAirspace, Resupply, Recon }

    public enum ObjectiveSource { Player, Auto }

    /// <summary>
    /// A strategic goal: a kind + a place (+ optional target) + a priority. The brain turns objectives into
    /// phased <see cref="Operation"/>s. Pure data — no Unity, no game refs. Maps to the existing per-unit
    /// tasking vocabulary (<see cref="OrderKind"/>/<see cref="DomainSet"/>) so execution reuses the planner.
    /// </summary>
    public sealed class Objective
    {
        public string Id { get; }
        /// <summary>Settable so the player can re-type/move an objective in place — the live operation shares
        /// this reference, so the change is seen without desyncing tasking/phase logic.</summary>
        public ObjectiveKind Kind { get; set; }
        public Vec3 Position { get; set; }
        public string TargetId { get; }
        public float Priority { get; set; }
        public ObjectiveSource Source { get; }

        public Objective(string id, ObjectiveKind kind, Vec3 position, ObjectiveSource source,
            string targetId = null, float priority = 1f)
        {
            Id = id;
            Kind = kind;
            Position = position;
            Source = source;
            TargetId = targetId;
            Priority = priority;
        }

        public OrderKind ToOrderKind()
        {
            switch (Kind)
            {
                case ObjectiveKind.CapturePoint: return OrderKind.Capture;
                case ObjectiveKind.DestroyTarget: return OrderKind.Attack;
                case ObjectiveKind.DefendArea: return OrderKind.Defend;
                case ObjectiveKind.ControlAirspace: return OrderKind.Defend; // CAP / air defense over a zone
                case ObjectiveKind.Resupply: return OrderKind.Resupply;
                case ObjectiveKind.Recon: return OrderKind.Move;
                default: return OrderKind.Move;
            }
        }

        public DomainSet ToDomains()
        {
            switch (Kind)
            {
                case ObjectiveKind.CapturePoint:
                case ObjectiveKind.Resupply: return DomainSet.Land | DomainSet.Sea;   // surface forces
                case ObjectiveKind.ControlAirspace: return DomainSet.Air;             // aircraft only
                default: return DomainSet.All;
            }
        }
    }
}
