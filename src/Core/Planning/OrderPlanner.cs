using System.Collections.Generic;
using System.Linq;
using CommanderLayer.Core.Model;

namespace CommanderLayer.Core.Planning
{
    /// <summary>
    /// Pure selection logic: given an order, the friendly roster, the threat picture, and config, choose a
    /// *suitable subset* of units and emit their tasks. This is what replaces the "everyone stampedes"
    /// behavior — only role-appropriate, in-range, commandable units are tasked. Fully unit-testable.
    /// P1 scope: ground/ship Attack + Defend (aircraft join in P4 via the AI-intent path).
    /// </summary>
    public static class OrderPlanner
    {
        public static TaskPlan Plan(CommanderOrder order, IReadOnlyList<UnitView> roster,
            ThreatPicture threat, CommanderConfig cfg)
        {
            var candidates = roster
                .Where(u => u != null && !u.Disabled && u.Commandable)
                .Where(u => Suitable(u, order.Kind))
                .Select(u => (u, dist: u.Position.HorizontalDistanceTo(order.Position)))
                .Where(x => x.dist <= cfg.SelectionRadius)
                .OrderBy(x => x.dist)
                .Take(cfg.MaxUnitsPerOrder)
                .Select(x => x.u)
                .ToList();

            var tasks = new List<UnitTask>(candidates.Count);
            foreach (var u in candidates)
            {
                switch (order.Kind)
                {
                    case OrderKind.Attack:
                        if (order.TargetId != null && u.Cap.CanEngageGround)
                            tasks.Add(new UnitTask(u.Id, TaskVerb.AttackTarget, order.Position, order.TargetId));
                        else
                            tasks.Add(new UnitTask(u.Id, TaskVerb.MoveTo, order.Position));
                        break;
                    case OrderKind.Defend:
                        // Move to cover the area; the manager flips arrivals to Hold (P3 refinement).
                        tasks.Add(new UnitTask(u.Id, TaskVerb.MoveTo, order.Position));
                        break;
                }
            }
            return new TaskPlan(order.Id, tasks);
        }

        /// <summary>Role/capability suitability per order kind (excludes supply/radar/transport/carrier/UGV).</summary>
        public static bool Suitable(UnitView u, OrderKind kind)
        {
            switch (kind)
            {
                case OrderKind.Attack:
                    // combat units that can hit ground targets
                    return u.Cap.CanEngageGround;
                case OrderKind.Defend:
                    // air-defense to cover, plus ground-capable combat units to garrison
                    return u.Cap.IsAirDefense || u.Cap.CanEngageGround;
                default:
                    return false;
            }
        }
    }
}
