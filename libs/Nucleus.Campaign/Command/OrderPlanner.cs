using System.Collections.Generic;
using Nucleus.Core.Model;

namespace Nucleus.Core.Command
{
    /// <summary>One child node of an <see cref="OrderPlan"/>: a kind, its place (the centroid of the threat it
    /// addresses), and the sibling indices it waits on. Ids are assigned later (by the brain, where the monotonic
    /// seed lives) so the planner stays pure + id-agnostic.</summary>
    public sealed class ChildObjectiveSpec
    {
        public ObjectiveKind Kind { get; }
        public Vec3 Position { get; }
        /// <summary>Indices into <see cref="OrderPlan.Children"/> that must resolve before this node fields.</summary>
        public IReadOnlyList<int> DependsOnIndices { get; }

        public ChildObjectiveSpec(ObjectiveKind kind, Vec3 position, IReadOnlyList<int> dependsOnIndices = null)
        {
            Kind = kind;
            Position = position;
            DependsOnIndices = dependsOnIndices ?? System.Array.Empty<int>();
        }
    }

    /// <summary>A decomposed goal: the prerequisite children, then the goal itself as the last child (depending
    /// on every prerequisite). Id-free — the brain materializes it into <see cref="Objective"/>s + an
    /// <see cref="Order"/>.</summary>
    public sealed class OrderPlan
    {
        public ObjectiveKind GoalKind { get; }
        public Vec3 Position { get; }
        public float Priority { get; }
        public ObjectiveSource Source { get; }
        /// <summary>Optional specific target for the goal child (a player drop on a named unit); null otherwise.</summary>
        public string TargetId { get; }
        public IReadOnlyList<ChildObjectiveSpec> Children { get; }
        /// <summary>The goal is always the last child.</summary>
        public int GoalIndex => Children.Count - 1;

        public OrderPlan(ObjectiveKind goalKind, Vec3 position, float priority, ObjectiveSource source,
            IReadOnlyList<ChildObjectiveSpec> children, string targetId = null)
        {
            GoalKind = goalKind;
            Position = position;
            Priority = priority;
            Source = source;
            TargetId = targetId;
            Children = children;
        }
    }

    /// <summary>
    /// Pure decomposition: turns a goal + the threat picture at its place into the prerequisite objectives the
    /// threat actually demands, each sited on the threat it addresses (so the force is sent where the work is) and
    /// sequenced by dependency. Radar/fuzzy intel → Recon; enemy air → ControlAirspace; a SAM belt thicker than
    /// doctrine tolerates → SuppressAirDefense; enemy ships → NavalStrike; then the goal, gated on all of them. An
    /// undefended goal — or one whose only air defence the phase engine already tolerates — decomposes to exactly
    /// itself, structurally identical to the pre-order behaviour, so the baseline (and the determinism canary) is
    /// preserved. No id allocation, no Unity, no clock/RNG.
    /// </summary>
    public static class OrderPlanner
    {
        public static OrderPlan Decompose(ObjectiveKind goalKind, Vec3 position, float priority,
            ObjectiveSource source, ThreatPicture threat, Doctrine doctrine = null, string targetId = null)
        {
            doctrine = doctrine ?? new Doctrine();
            var children = new List<ChildObjectiveSpec>();
            var prereqs = new List<int>();

            // A prerequisite is only added when its threat is present AND it isn't the goal itself (a first-class
            // ControlAirspace / SuppressAirDefense / NavalStrike order must not spawn a same-kind prerequisite).
            void AddPrereq(ObjectiveKind kind, Vec3 at)
            {
                if (kind == goalKind) return;
                children.Add(new ChildObjectiveSpec(kind, at));
                prereqs.Add(children.Count - 1);
            }

            if (threat != null)
            {
                bool fuzzy = false;
                foreach (var e in threat.Enemies) if (!e.Accurate) { fuzzy = true; break; }
                if (threat.HasRadar || fuzzy) AddPrereq(ObjectiveKind.Recon, position);
                if (threat.AirCount > 0)
                    AddPrereq(ObjectiveKind.ControlAirspace, Centroid(threat, position, IsAir));
                // Only dedicate SEAD when the belt is thicker than the phase engine will fly through.
                if (threat.AirDefenseCount > doctrine.MaxResidualAirDefense)
                    AddPrereq(ObjectiveKind.SuppressAirDefense, Centroid(threat, position, IsAirDefense));
                if (threat.NavalCount > 0)
                    AddPrereq(ObjectiveKind.NavalStrike, Centroid(threat, position, IsNaval));
            }

            // The goal waits on every prerequisite (empty when undefended → a lone goal node).
            children.Add(new ChildObjectiveSpec(goalKind, position, prereqs.ToArray()));
            return new OrderPlan(goalKind, position, priority, source, children, targetId);
        }

        private static bool IsAir(EnemyView e) => e.Class == UnitClass.Aircraft;
        private static bool IsAirDefense(EnemyView e) => e.Cap.IsAirDefense;
        private static bool IsNaval(EnemyView e) =>
            e.Cap.Role == Role.Carrier || e.Cap.Role == Role.CombatShip || e.Cap.Role == Role.TransportShip;

        // Centroid of the matching contacts (the place to send the force); falls back to the goal point if none.
        private static Vec3 Centroid(ThreatPicture threat, Vec3 fallback, System.Func<EnemyView, bool> match)
        {
            float x = 0f, y = 0f, z = 0f;
            int n = 0;
            foreach (var e in threat.Enemies)
                if (match(e)) { x += e.Position.X; y += e.Position.Y; z += e.Position.Z; n++; }
            return n == 0 ? fallback : new Vec3(x / n, y / n, z / n);
        }
    }
}
