using System;
using System.Collections.Generic;
using System.Linq;
using CommanderLayer.Core.Model;

namespace CommanderLayer.Core.Planning
{
    /// <summary>
    /// Stateful (but engine-free, testable) manager of active orders. Plans on add, and on each management
    /// tick validates assignments, reassigns on loss (config), and completes orders whose goal is met. All
    /// inputs are plain snapshots/delegates so the Game layer supplies live data and executes the returned
    /// tasks. No Unity types here.
    /// </summary>
    public sealed class AssignmentManager
    {
        private readonly CommanderConfig _cfg;
        private readonly List<OrderState> _orders = new List<OrderState>();

        public AssignmentManager(CommanderConfig cfg)
        {
            _cfg = cfg ?? new CommanderConfig();
        }

        public IReadOnlyList<OrderState> Orders => _orders;
        public CommanderConfig Config => _cfg;

        /// <summary>Plan a new order, record assignments, and return the initial tasks to execute.</summary>
        public TaskPlan AddOrder(CommanderOrder order, IReadOnlyList<UnitView> roster, ThreatPicture threat)
        {
            var plan = OrderPlanner.Plan(order, roster, threat, _cfg);
            var state = new OrderState(order);
            Record(state, plan);
            // An Air-domain order steers aircraft via the AI patch (not commandable units), so an empty
            // ground/ship plan is still Active — only a non-air order with no units truly fails.
            bool hasAir = (order.Domains & DomainSet.Air) != 0;
            state.Status = (!plan.IsEmpty || hasAir) ? OrderStatus.Active : OrderStatus.Failed;
            if (plan.IsEmpty && hasAir) state.Summary = "Air tasking: idle aircraft will ingress.";
            else state.Summary = Summarize(state, threat);
            _orders.Add(state);
            return plan;
        }

        /// <summary>
        /// Management tick: drop lost units, complete finished orders, reassign empties. Returns any tasks
        /// that must be (re)issued this tick (e.g. from reassignment). <paramref name="threatFor"/> yields
        /// the current fog-of-war threat near a given order.
        /// </summary>
        public IReadOnlyList<UnitTask> Tick(IReadOnlyList<UnitView> roster, Func<CommanderOrder, ThreatPicture> threatFor,
            Func<CommanderOrder, bool> captureDone = null)
        {
            var alive = new HashSet<string>();
            var posById = new Dictionary<string, Vec3>();
            foreach (var u in roster)
            {
                if (u != null && !u.Disabled) { alive.Add(u.Id); posById[u.Id] = u.Position; }
            }

            var toIssue = new List<UnitTask>();
            foreach (var s in _orders)
            {
                if (s.Status == OrderStatus.Complete || s.Status == OrderStatus.Failed) continue;

                s.AssignedUnitIds.RemoveAll(id => !alive.Contains(id));
                var threat = threatFor != null ? threatFor(s.Order) : ThreatPicture.Empty;

                // Completion: an Attack is done when no known enemies remain AND we've actually contested the
                // area — i.e. an assigned unit reached it. Gating on arrival avoids a false "clear" that
                // fires merely because the enemy slipped out of our fog-of-war sight before we engaged.
                // Air-only orders (no commandable units assigned) fall back to the intel-clear signal.
                if (s.Order.Kind == OrderKind.Attack && threat.Count == 0)
                {
                    bool unitInArea = s.AssignedUnitIds.Any(id =>
                        posById.TryGetValue(id, out var p) && p.HorizontalDistanceTo(s.Order.Position) <= _cfg.ArriveRadius);
                    if (unitInArea || s.AssignedUnitIds.Count == 0)
                    {
                        s.Status = OrderStatus.Complete;
                        s.Summary = "Area secure.";
                        continue;
                    }
                }

                // Completion: a Move is done once its assigned units have reached the point.
                if (s.Order.Kind == OrderKind.Move && s.AssignedUnitIds.Count > 0
                    && s.AssignedUnitIds.All(id => posById.TryGetValue(id, out var p)
                        && p.HorizontalDistanceTo(s.Order.Position) <= _cfg.ArriveRadius))
                {
                    s.Status = OrderStatus.Complete;
                    s.Summary = "In position.";
                    continue;
                }

                // Completion: a Capture whose objective now flies our flag is done (game-supplied poll).
                if (s.Order.Kind == OrderKind.Capture && captureDone != null && captureDone(s.Order))
                {
                    s.Status = OrderStatus.Complete;
                    s.Summary = "Objective captured.";
                    continue;
                }

                // Reassign if we've lost the whole assignment and auto-reassign is on.
                if (_cfg.AutoReassign && s.AssignedUnitIds.Count == 0)
                {
                    var plan = OrderPlanner.Plan(s.Order, roster, threat, _cfg);
                    Record(s, plan);
                    foreach (var t in plan.Tasks) toIssue.Add(t);
                    s.Status = plan.IsEmpty ? OrderStatus.Failed : OrderStatus.Active;
                }

                // Defend: once a unit reaches the area, tell it to hold (garrison) — issued once.
                if (s.Order.Kind == OrderKind.Defend && s.Status == OrderStatus.Active)
                {
                    foreach (var id in s.AssignedUnitIds)
                    {
                        if (s.Held.Contains(id)) continue;
                        if (posById.TryGetValue(id, out var p) && p.HorizontalDistanceTo(s.Order.Position) <= _cfg.ArriveRadius)
                        {
                            toIssue.Add(new UnitTask(id, TaskVerb.Hold, s.Order.Position));
                            s.Held.Add(id);
                        }
                    }
                }

                s.Summary = Summarize(s, threat);
            }
            return toIssue;
        }

        /// <summary>Add a pre-built order state (e.g. a one-shot Build commission) without planning units.</summary>
        public void AddExisting(OrderState state) => _orders.Add(state);

        public bool Clear(string orderId)
        {
            return _orders.RemoveAll(o => o.Order.Id == orderId) > 0;
        }

        public void ClearAll() => _orders.Clear();

        private static void Record(OrderState state, TaskPlan plan)
        {
            foreach (var t in plan.Tasks)
            {
                if (!state.AssignedUnitIds.Contains(t.UnitId))
                {
                    state.AssignedUnitIds.Add(t.UnitId);
                }
            }
        }

        private static string Summarize(OrderState s, ThreatPicture threat)
        {
            if (s.Status == OrderStatus.Failed) return "No suitable units available.";
            string kind = s.Order.Kind.ToString();
            return $"{kind}: {s.AssignedUnitIds.Count} unit(s) assigned, {threat.Count} threat(s) known.";
        }
    }
}
