using System.Collections.Generic;
using CommanderLayer.Core.Model;
using CommanderLayer.Core.Planning;

namespace CommanderLayer.Game
{
    /// <summary>
    /// Orchestrates the commander: gathers the roster + fog-of-war threat, runs the pure planner/manager,
    /// and executes the resulting per-unit commands. This is the seam between Core logic and the game.
    /// </summary>
    public sealed class CommanderService
    {
        private readonly CommanderConfig _cfg;
        private readonly AssignmentManager _mgr;
        private readonly GameRoster _roster = new GameRoster();
        private readonly GameIntel _intel = new GameIntel();
        private readonly GameUnitCommands _cmds = new GameUnitCommands();
        private readonly GameProduction _production = new GameProduction();
        private readonly GameCapture _capture = new GameCapture();
        private int _counter;

        public CommanderService(CommanderConfig cfg)
        {
            _cfg = cfg ?? new CommanderConfig();
            _mgr = new AssignmentManager(_cfg);
        }

        public CommanderConfig Config => _cfg;
        public IReadOnlyList<OrderState> Orders => _mgr.Orders;
        /// <summary>Roster from the last Place/Tick (refreshed on the throttled management loop).</summary>
        public IReadOnlyList<UnitView> LastRoster { get; private set; } = new List<UnitView>();

        /// <summary>Place an order at a world point: plan a suitable subset and command them. Host-side.</summary>
        public OrderState PlaceOrder(OrderKind kind, Vec3 world, DomainSet domains, float radius)
        {
            // Build = commission-only: queue production at the base; no unit tasking, no rally.
            if (kind == OrderKind.Build)
            {
                string result = _production.Commission();
                var bo = new CommanderOrder("ord-" + (++_counter), kind, world, 0f, domains);
                var bs = new OrderState(bo) { Status = OrderStatus.Complete, Summary = result };
                _mgr.AddExisting(bs);
                Plugin.Log?.LogInfo($"Build commission: {result}");
                return bs;
            }

            var roster = _roster.BuildRoster();
            LastRoster = roster;
            float r = radius > 0f ? radius : _cfg.SelectionRadius;
            var threat = ThreatAssessor.Assess(_intel.KnownEnemiesNear(world, r));
            var order = new CommanderOrder("ord-" + (++_counter), kind, world, r, domains);
            var plan = _mgr.AddOrder(order, roster, threat);
            foreach (var t in plan.Tasks) _cmds.Execute(t);
            RefreshAirIntent();
            Plugin.Log?.LogInfo($"Order {order.Id} ({kind}, {domains}, r={r:0}) at {world}: {plan.Tasks.Count} unit(s) tasked.");
            return _mgr.Orders[_mgr.Orders.Count - 1];
        }

        /// <summary>Live preview of who'd be assigned at a hover point (uses the cached roster).</summary>
        public AssignmentPreview PreviewAt(OrderKind kind, Vec3 world, DomainSet domains, float radius)
        {
            if (LastRoster.Count == 0) LastRoster = _roster.BuildRoster();
            float r = radius > 0f ? radius : _cfg.SelectionRadius;
            var threat = ThreatAssessor.Assess(_intel.KnownEnemiesNear(world, r));
            var order = new CommanderOrder("preview", kind, world, r, domains);
            return OrderPlanner.Preview(order, LastRoster, threat, _cfg);
        }

        /// <summary>Management tick (throttled by the runtime): validate/reassign/complete, re-issue tasks.</summary>
        public void Tick()
        {
            var roster = _roster.BuildRoster();
            LastRoster = roster;
            var reissue = _mgr.Tick(roster,
                o => ThreatAssessor.Assess(_intel.KnownEnemiesNear(o.Position, _cfg.ThreatRadius)),
                o => _capture.IsHeldByUs(o.Position));
            foreach (var t in reissue) _cmds.Execute(t);
            RefreshAirIntent();
        }

        // Publish the Air-domain order points as aircraft ingress zones (consumed by the NoTarget patch).
        // SEAD-before-strike: withhold a zone while known enemy air defenses remain there, so aircraft
        // only ingress once SAMs/AAA are suppressed by the order's ground/sea element.
        private void RefreshAirIntent()
        {
            var zones = new List<Vec3>();
            foreach (var o in _mgr.Orders)
            {
                if (o.Status == OrderStatus.Complete || o.Status == OrderStatus.Failed) continue;
                if ((o.Order.Domains & DomainSet.Air) == 0) continue;
                var threat = ThreatAssessor.Assess(_intel.KnownEnemiesNear(o.Order.Position, _cfg.ThreatRadius));
                if (OrderPlanner.SeadPending(o.Order, threat)) continue; // hold aircraft until air defenses fall
                zones.Add(o.Order.Position);
            }
            AircraftIntent.SetZones(zones);
        }

        public IReadOnlyList<UnitView> CurrentRoster() => _roster.BuildRoster();
        public void Clear(string orderId) => _mgr.Clear(orderId);
        public void ClearAll() => _mgr.ClearAll();
    }
}
