using System.Collections.Generic;
using System.Linq;

namespace CommanderLayer.Core.Command
{
    /// <summary>
    /// Flattened, render-ready view of one <see cref="Operation"/> for the UI. Carries only the fields a
    /// panel row needs (no object graph to walk), so the UI layer stays logic-free. Pure value type.
    /// </summary>
    public readonly struct OperationView
    {
        public string Id { get; }
        public ObjectiveKind Kind { get; }
        public CombatPhase Phase { get; }
        public OperationStatus Status { get; }
        public int SquadCount { get; }
        public AutonomyLevel Autonomy { get; }

        public OperationView(string id, ObjectiveKind kind, CombatPhase phase, OperationStatus status,
            int squadCount, AutonomyLevel autonomy)
        {
            Id = id;
            Kind = kind;
            Phase = phase;
            Status = status;
            SquadCount = squadCount;
            Autonomy = autonomy;
        }
    }

    /// <summary>Flattened, render-ready view of one <see cref="Squad"/> for the UI. Pure value type.</summary>
    public readonly struct SquadView
    {
        public string Id { get; }
        public string Name { get; }
        public RoleFamily Family { get; }
        public int Strength { get; }
        public SquadStatus Status { get; }
        public string AssignedOperationId { get; }
        public AutonomyLevel Autonomy { get; }
        /// <summary>What the squad is doing right now, e.g. "DestroyTarget — Strike" or "Reserve". For the UI.</summary>
        public string Activity { get; }

        public SquadView(string id, string name, RoleFamily family, int strength, SquadStatus status,
            string assignedOperationId, AutonomyLevel autonomy, string activity)
        {
            Id = id;
            Name = name;
            Family = family;
            Strength = strength;
            Status = status;
            AssignedOperationId = assignedOperationId;
            Autonomy = autonomy;
            Activity = activity ?? "";
        }
    }

    /// <summary>
    /// An immutable, render-ready snapshot of the whole HQ for the UI: operations, squads, production status
    /// lines, the recent battle feed and the commander's autonomy. One read of this is everything a panel
    /// draws — no game/state references leak into the view layer. Pure Core.
    /// </summary>
    public sealed class HqSnapshot
    {
        public IReadOnlyList<OperationView> Operations { get; }
        public IReadOnlyList<SquadView> Squads { get; }
        public IReadOnlyList<string> Production { get; }
        public IReadOnlyList<ReportEvent> Recent { get; }
        public AutonomyLevel CommanderAutonomy { get; }
        /// <summary>Pending Assisted suggestions the player can confirm (empty unless the commander is Assisted).</summary>
        public IReadOnlyList<Proposal> Proposals { get; }

        public HqSnapshot(IReadOnlyList<OperationView> operations, IReadOnlyList<SquadView> squads,
            IReadOnlyList<string> production, IReadOnlyList<ReportEvent> recent, AutonomyLevel commanderAutonomy,
            IReadOnlyList<Proposal> proposals = null)
        {
            Operations = operations;
            Squads = squads;
            Production = production;
            Recent = recent;
            CommanderAutonomy = commanderAutonomy;
            Proposals = proposals ?? new List<Proposal>();
        }
    }

    /// <summary>
    /// Pure projection from live commander state to a flat <see cref="HqSnapshot"/> the UI can render without
    /// any logic of its own. Null-safe: a missing log or production queue maps to empty lists. The one place
    /// where "what the screen shows" is computed.
    /// </summary>
    public static class HqView
    {
        public static HqSnapshot Build(CommanderState state, BattleLog log, ProductionQueue production,
            int recentCount = 10)
        {
            var operations = state.Operations
                .Select(op => new OperationView(
                    op.Id, op.Objective.Kind, op.CombatPhase, op.Status, op.SquadIds.Count, op.Autonomy))
                .ToList();

            var squads = state.Squads.Squads
                .Select(s => new SquadView(
                    s.Id, s.Name, s.Family, s.Strength, s.Status, s.AssignedOperationId, s.Autonomy,
                    SquadActivity(s, state)))
                .ToList();

            var productionLines = production != null
                ? production.Describe()
                : (IReadOnlyList<string>)new List<string>();

            var recent = log != null
                ? log.Recent(recentCount)
                : (IReadOnlyList<ReportEvent>)new List<ReportEvent>();

            return new HqSnapshot(operations, squads, productionLines, recent, state.Autonomy,
                state.Proposals.ToList());
        }

        /// <summary>What a squad is doing: its operation's objective + combat phase if engaged, else its
        /// status (Reserve/Forming/Ready). Manual squads are flagged as player-held.</summary>
        private static string SquadActivity(Squad s, CommanderState state)
        {
            if (s.Autonomy == AutonomyLevel.Manual) return "YOURS (manual)";
            if (!string.IsNullOrEmpty(s.AssignedOperationId))
            {
                var op = state.Operations.Find(o => o.Id == s.AssignedOperationId);
                if (op != null) return $"{op.Objective.Kind} — {op.CombatPhase}";
            }
            return s.Status.ToString();
        }
    }
}
