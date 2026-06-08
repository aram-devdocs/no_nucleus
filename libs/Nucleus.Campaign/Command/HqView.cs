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

    /// <summary>One node of an order tree (a child objective), flattened for the command-center panel + map.</summary>
    public readonly struct OrderNodeView
    {
        public string ObjectiveId { get; }
        public ObjectiveKind Kind { get; }
        /// <summary>Live combat phase of this node's operation (Recon when not yet fielded) — shown in the detail pane.</summary>
        public CombatPhase Phase { get; }
        public int SquadCount { get; }
        public AutonomyLevel Autonomy { get; }
        public Vec3 Position { get; }
        public bool IsGoal { get; }
        /// <summary>This node has a live operation working it.</summary>
        public bool Active { get; }
        /// <summary>The goal/prereq is achieved (its objective has been retired).</summary>
        public bool Complete { get; }
        /// <summary>All prerequisite siblings are resolved, so this node may field. Goal nodes start blocked.</summary>
        public bool DependenciesMet { get; }
        /// <summary>No available force family can carry this node (domain-aware reachability badge).</summary>
        public bool Unreachable { get; }

        public OrderNodeView(string objectiveId, ObjectiveKind kind, CombatPhase phase, int squadCount,
            AutonomyLevel autonomy, Vec3 position, bool isGoal, bool active, bool complete, bool dependenciesMet,
            bool unreachable)
        {
            ObjectiveId = objectiveId;
            Kind = kind;
            Phase = phase;
            SquadCount = squadCount;
            Autonomy = autonomy;
            Position = position;
            IsGoal = isGoal;
            Active = active;
            Complete = complete;
            DependenciesMet = dependenciesMet;
            Unreachable = unreachable;
        }
    }

    /// <summary>A parent order + its decomposed child nodes — the command-center's tree row + selection detail.</summary>
    public readonly struct OrderView
    {
        public string Id { get; }
        public ObjectiveKind GoalKind { get; }
        public OrderStatus Status { get; }
        public AutonomyLevel Autonomy { get; }
        public bool PlayerOwned { get; }
        public Vec3 Position { get; }
        public float Priority { get; }
        /// <summary>The goal objective's id — what selecting the parent row addresses.</summary>
        public string GoalObjectiveId { get; }
        public IReadOnlyList<OrderNodeView> Nodes { get; }

        public OrderView(string id, ObjectiveKind goalKind, OrderStatus status, AutonomyLevel autonomy,
            bool playerOwned, Vec3 position, float priority, string goalObjectiveId, IReadOnlyList<OrderNodeView> nodes)
        {
            Id = id;
            GoalKind = goalKind;
            Status = status;
            Autonomy = autonomy;
            PlayerOwned = playerOwned;
            Position = position;
            Priority = priority;
            GoalObjectiveId = goalObjectiveId;
            Nodes = nodes;
        }
    }

    /// <summary>Immutable, render-ready snapshot of the whole HQ — everything a panel draws in one read, with
    /// no game/state references leaking into the view layer.</summary>
    public sealed class HqSnapshot
    {
        public IReadOnlyList<OperationView> Operations { get; }
        public IReadOnlyList<OrderView> Orders { get; }
        public IReadOnlyList<SquadView> Squads { get; }
        public IReadOnlyList<string> Production { get; }
        public IReadOnlyList<ReportEvent> Recent { get; }
        public bool AiCreatesObjectives { get; }
        public bool AiAutoFill { get; }
        /// <summary>Total queued production cost — drives the build panel's "Funds · Queued · After" warning.</summary>
        public float QueuedCost { get; }

        public HqSnapshot(IReadOnlyList<OperationView> operations, IReadOnlyList<SquadView> squads,
            IReadOnlyList<string> production, IReadOnlyList<ReportEvent> recent,
            bool aiCreatesObjectives, bool aiAutoFill, float queuedCost = 0f,
            IReadOnlyList<OrderView> orders = null)
        {
            Operations = operations;
            Orders = orders ?? System.Array.Empty<OrderView>();
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
                state.AiCreatesObjectives, state.AiAutoFill, production?.QueuedCost ?? 0f, BuildOrders(state));
        }

        // Flatten each order into a parent + child nodes. Retired prerequisites (resolved + pruned) drop off the
        // tree; a retired goal stays as a Complete node for the fade window so the panel can show the win.
        private static IReadOnlyList<OrderView> BuildOrders(CommanderState state)
        {
            if (state.Orders.Count == 0) return System.Array.Empty<OrderView>();
            var orders = new List<OrderView>(state.Orders.Count);
            foreach (var ord in state.Orders)
            {
                var nodes = new List<OrderNodeView>(ord.ChildObjectiveIds.Count);
                foreach (var cid in ord.ChildObjectiveIds)
                {
                    bool isGoal = cid == ord.GoalObjectiveId;
                    var obj = state.Objectives.Find(o => o.Id == cid);
                    if (obj == null)
                    {
                        if (isGoal)
                            nodes.Add(new OrderNodeView(cid, ord.GoalKind, CombatPhase.Recon, 0, ord.Autonomy,
                                ord.Position, true, false, true, true, false));
                        continue;   // a retired prerequisite is done — drop it from the live tree
                    }
                    var op = state.OperationFor(cid);
                    bool depsMet = true;
                    foreach (var dep in obj.DependsOn)
                        if (state.Objectives.Exists(o => o.Id == dep)) { depsMet = false; break; }
                    nodes.Add(new OrderNodeView(cid, obj.Kind, op?.CombatPhase ?? CombatPhase.Recon,
                        op?.SquadIds.Count ?? 0, op?.Autonomy ?? ord.Autonomy, obj.Position, isGoal,
                        op != null && op.Status == OperationStatus.Active, false, depsMet, false));
                }
                orders.Add(new OrderView(ord.Id, ord.GoalKind, ord.Status, ord.Autonomy,
                    ord.Source == ObjectiveSource.Player, ord.Position, ord.Priority, ord.GoalObjectiveId, nodes));
            }
            return orders;
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

        // One format with a trailing [AI]/[YOU] badge so a glance reads both what the squad is doing AND who
        // controls it, without cross-referencing the autonomy button.
        private static string SquadActivity(Squad s, CommanderState state)
        {
            string who = s.Autonomy == AutonomyLevel.Manual ? "YOU" : "AI";
            if (!string.IsNullOrEmpty(s.AssignedOperationId))
            {
                var op = state.Operations.Find(o => o.Id == s.AssignedOperationId);
                if (op != null)
                    return $"{ObjectiveText.Name(op.Objective.Kind)} — {ObjectiveText.PhaseLabel(op.CombatPhase)} [{who}]";
            }
            return $"{s.Status} [{who}]";
        }
    }
}
