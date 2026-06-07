using System.Collections.Generic;
using System.Linq;
using Nucleus.Core.Model;

namespace Nucleus.Core.Command
{
    /// <summary>Flattened, render-ready view of one <see cref="Operation"/> and its driving objective — no
    /// object graph to walk, so the UI layer stays logic-free.</summary>
    public readonly struct OperationView
    {
        public string Id { get; }
        /// <summary>The driving objective's id (what CreateObjective returned / Edit/Move/Remove address).</summary>
        public string ObjectiveId { get; }
        public ObjectiveKind Kind { get; }
        public CombatPhase Phase { get; }
        public OperationStatus Status { get; }
        public int SquadCount { get; }
        public AutonomyLevel Autonomy { get; }
        public Vec3 Position { get; }
        public float Priority { get; }
        /// <summary>True when the player dropped this objective (vs. AI-generated).</summary>
        public bool PlayerOwned { get; }
        /// <summary>Enemies counted when the operation opened — how defended the objective is.</summary>
        public int ThreatCount { get; }
        /// <summary>Of those, how many are air defenses (the "need SEAD" signal).</summary>
        public int ThreatAirDefense { get; }

        public OperationView(string id, ObjectiveKind kind, CombatPhase phase, OperationStatus status,
            int squadCount, AutonomyLevel autonomy, string? objectiveId = null, Vec3 position = default,
            float priority = 1f, bool playerOwned = false, int threatCount = 0, int threatAirDefense = 0)
        {
            Id = id;
            Kind = kind;
            Phase = phase;
            Status = status;
            SquadCount = squadCount;
            Autonomy = autonomy;
            ObjectiveId = objectiveId ?? id;
            Position = position;
            Priority = priority;
            PlayerOwned = playerOwned;
            ThreatCount = threatCount;
            ThreatAirDefense = threatAirDefense;
        }
    }

    /// <summary>Flattened, render-ready view of one <see cref="Squad"/>.</summary>
    public readonly struct SquadView
    {
        public string Id { get; }
        public string Name { get; }
        public RoleFamily Family { get; }
        public int Strength { get; }
        public SquadStatus Status { get; }
        /// <summary>Flag so the Ui lib can red-flag a hurt squad without referencing SquadStatus (outside its deps).</summary>
        public bool Depleted => Status == SquadStatus.Depleted;
        public string? AssignedOperationId { get; }
        public AutonomyLevel Autonomy { get; }
        /// <summary>What the squad is doing now, e.g. "Destroy target — Strike" or "Reserve".</summary>
        public string Activity { get; }
        public IReadOnlyList<string> MemberUnitIds { get; }
        /// <summary>Human composition "2× MBT, 1× IFV"; empty when no roster role map was supplied.</summary>
        public string Composition { get; }
        /// <summary>Target strength for "have/need" readouts; 0 when unknown.</summary>
        public int TargetStrength { get; }

        public SquadView(string id, string name, RoleFamily family, int strength, SquadStatus status,
            string? assignedOperationId, AutonomyLevel autonomy, string activity, IReadOnlyList<string>? memberUnitIds = null,
            string composition = "", int targetStrength = 0)
        {
            Id = id;
            Name = name;
            Family = family;
            Strength = strength;
            Status = status;
            AssignedOperationId = assignedOperationId;
            Autonomy = autonomy;
            Activity = activity ?? "";
            MemberUnitIds = memberUnitIds ?? new List<string>();
            Composition = composition ?? "";
            TargetStrength = targetStrength;
        }
    }

    /// <summary>Immutable, render-ready snapshot of the whole HQ — everything a panel draws in one read, with
    /// no game/state references leaking into the view layer.</summary>
    public sealed class HqSnapshot
    {
        public IReadOnlyList<OperationView> Operations { get; }
        public IReadOnlyList<SquadView> Squads { get; }
        public IReadOnlyList<string> Production { get; }
        public IReadOnlyList<ReportEvent> Recent { get; }
        public bool AiCreatesObjectives { get; }
        public bool AiAutoFill { get; }
        /// <summary>Total queued production cost — drives the build panel's "Funds · Queued · After" warning.</summary>
        public float QueuedCost { get; }

        public HqSnapshot(IReadOnlyList<OperationView> operations, IReadOnlyList<SquadView> squads,
            IReadOnlyList<string> production, IReadOnlyList<ReportEvent> recent,
            bool aiCreatesObjectives, bool aiAutoFill, float queuedCost = 0f)
        {
            Operations = operations;
            Squads = squads;
            Production = production;
            Recent = recent;
            AiCreatesObjectives = aiCreatesObjectives;
            AiAutoFill = aiAutoFill;
            QueuedCost = queuedCost;
        }
    }

    /// <summary>Pure projection from live commander state to a flat <see cref="HqSnapshot"/>. Null-safe: a
    /// missing log or production queue maps to empty lists.</summary>
    public static class HqView
    {
        public static HqSnapshot Build(CommanderState state, BattleLog log, ProductionQueue production,
            int recentCount = 10, IReadOnlyDictionary<string, Role> unitRoles = null)
        {
            // Pre-sized manual loops, no LINQ — Build runs on the render path, so per-render garbage adds up.
            var operations = new List<OperationView>(state.Operations.Count + state.Objectives.Count);
            var withOps = new System.Collections.Generic.HashSet<string>();
            foreach (var op in state.Operations)
            {
                operations.Add(new OperationView(
                    op.Id, op.Objective.Kind, op.CombatPhase, op.Status, op.SquadIds.Count, op.Autonomy,
                    op.Objective.Id, op.Objective.Position, op.Objective.Priority,
                    op.Objective.Source == ObjectiveSource.Player,
                    op.InitialThreat?.Count ?? 0, op.InitialThreat?.AirDefenseCount ?? 0));
                withOps.Add(op.Objective.Id);
            }

            // A just-dropped objective has no Operation yet (one forms when a squad is assigned). Surface it as a
            // placeholder so it's immediately visible/selectable/movable, even with auto-fill OFF.
            foreach (var obj in state.Objectives)
            {
                if (withOps.Contains(obj.Id)) continue;
                operations.Add(new OperationView(
                    obj.Id, obj.Kind, CombatPhase.Recon, OperationStatus.Planning, 0, AutonomyLevel.Auto,
                    obj.Id, obj.Position, obj.Priority, obj.Source == ObjectiveSource.Player));
            }

            // Scratch dict reused across squads instead of one per squad. The MemberUnitIds copy is KEPT: the
            // snapshot outlives the tick and reconcile mutates that list, so aliasing it would be a footgun.
            var scratch = unitRoles != null ? new Dictionary<string, int>() : null;
            var squads = new List<SquadView>(state.Squads.Squads.Count);
            foreach (var s in state.Squads.Squads)
                squads.Add(new SquadView(
                    s.Id, s.Name, s.Family, s.Strength, s.Status, s.AssignedOperationId, s.Autonomy,
                    SquadActivity(s, state), new List<string>(s.MemberUnitIds),
                    CompositionLabel(s, unitRoles, scratch), s.TargetComposition?.Total ?? 0));

            var productionLines = production != null
                ? production.Describe()
                : (IReadOnlyList<string>)new List<string>();

            var recent = log != null
                ? log.Recent(recentCount)
                : (IReadOnlyList<ReportEvent>)new List<ReportEvent>();

            return new HqSnapshot(operations, squads, productionLines, recent,
                state.AiCreatesObjectives, state.AiAutoFill, production?.QueuedCost ?? 0f);
        }

        // "2× MBT, 1× IFV" (top 3 by count) from a unit-id->role map; empty when no map is supplied.
        private static string CompositionLabel(Squad s, IReadOnlyDictionary<string, Role> unitRoles, Dictionary<string, int> counts)
        {
            if (unitRoles == null || s.MemberUnitIds == null || s.MemberUnitIds.Count == 0) return "";
            counts.Clear();
            foreach (var id in s.MemberUnitIds)
            {
                if (!unitRoles.TryGetValue(id, out var role)) continue;
                var lbl = RoleLabels.Short(role);
                counts[lbl] = counts.TryGetValue(lbl, out var c) ? c + 1 : 1;
            }
            if (counts.Count == 0) return "";
            return string.Join(", ", counts
                .OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key)
                .Take(3)
                .Select(kv => $"{kv.Value}× {kv.Key}"));
        }

        private static string SquadActivity(Squad s, CommanderState state)
        {
            if (s.Autonomy == AutonomyLevel.Manual) return "YOURS (manual)";
            if (!string.IsNullOrEmpty(s.AssignedOperationId))
            {
                var op = state.Operations.Find(o => o.Id == s.AssignedOperationId);
                if (op != null) return $"{ObjectiveText.Name(op.Objective.Kind)} — {ObjectiveText.PhaseLabel(op.CombatPhase)}";
            }
            return s.Status.ToString();
        }
    }
}
