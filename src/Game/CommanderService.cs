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
        public OrderState PlaceOrder(OrderKind kind, Vec3 world)
        {
            var roster = _roster.BuildRoster();
            LastRoster = roster;
            var threat = ThreatAssessor.Assess(_intel.KnownEnemiesNear(world, _cfg.ThreatRadius));
            var order = new CommanderOrder("ord-" + (++_counter), kind, world, _cfg.ThreatRadius);
            var plan = _mgr.AddOrder(order, roster, threat);
            foreach (var t in plan.Tasks) _cmds.Execute(t);
            Plugin.Log?.LogInfo($"Order {order.Id} ({kind}) at {world}: {plan.Tasks.Count} unit(s) tasked.");
            return _mgr.Orders[_mgr.Orders.Count - 1];
        }

        /// <summary>Management tick (throttled by the runtime): validate/reassign/complete, re-issue tasks.</summary>
        public void Tick()
        {
            var roster = _roster.BuildRoster();
            LastRoster = roster;
            var reissue = _mgr.Tick(roster,
                o => ThreatAssessor.Assess(_intel.KnownEnemiesNear(o.Position, _cfg.ThreatRadius)));
            foreach (var t in reissue) _cmds.Execute(t);
        }

        public IReadOnlyList<UnitView> CurrentRoster() => _roster.BuildRoster();
        public void Clear(string orderId) => _mgr.Clear(orderId);
        public void ClearAll() => _mgr.ClearAll();
    }
}
