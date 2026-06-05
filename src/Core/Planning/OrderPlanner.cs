using System.Collections.Generic;
using System.Linq;
using CommanderLayer.Core.Model;

namespace CommanderLayer.Core.Planning
{
    /// <summary>
    /// Pure selection logic — replaces "everyone stampedes." Picks a suitable subset within the order's
    /// pull radius, filtered by the chosen domains (air/land/sea), excluding non-troops (missiles,
    /// buildings) and non-commandable units. SelectUnits is shared by Plan (to issue tasks) and Preview
    /// (for the live hover UI), so what you preview is exactly what gets tasked.
    /// </summary>
    public static class OrderPlanner
    {
        public static IReadOnlyList<UnitView> SelectUnits(CommanderOrder order, IReadOnlyList<UnitView> roster, CommanderConfig cfg)
        {
            float radius = order.Radius > 0f ? order.Radius : cfg.SelectionRadius;
            return roster
                .Where(u => u != null && !u.Disabled && u.Commandable)
                .Where(u => Suitable(u, order.Kind, order.Domains))
                .Select(u => (u, dist: u.Position.HorizontalDistanceTo(order.Position)))
                .Where(x => x.dist <= radius)
                .OrderBy(x => x.dist)
                .Take(cfg.MaxUnitsPerOrder)
                .Select(x => x.u)
                .ToList();
        }

        public static TaskPlan Plan(CommanderOrder order, IReadOnlyList<UnitView> roster, ThreatPicture threat, CommanderConfig cfg)
        {
            var selected = SelectUnits(order, roster, cfg);
            var tasks = new List<UnitTask>(selected.Count);
            foreach (var u in selected)
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
                    case OrderKind.Capture:
                    case OrderKind.Resupply:
                    case OrderKind.Move:
                        tasks.Add(new UnitTask(u.Id, TaskVerb.MoveTo, order.Position));
                        break;
                }
            }
            return new TaskPlan(order.Id, tasks);
        }

        /// <summary>Live "what would happen" without mutating state — for the hover preview.</summary>
        public static AssignmentPreview Preview(CommanderOrder order, IReadOnlyList<UnitView> roster, ThreatPicture threat, CommanderConfig cfg)
        {
            return new AssignmentPreview(SelectUnits(order, roster, cfg), threat);
        }

        /// <summary>
        /// SEAD-before-strike: an Air-domain order's aircraft must not ingress while known enemy air
        /// defenses (SAM/AAA) remain in the area — suppress first. True = hold the air wave. (The ground/sea
        /// part of the order does the suppressing; once the air-defense threat clears, aircraft are released.)
        /// </summary>
        public static bool SeadPending(CommanderOrder order, ThreatPicture threat)
            => (order.Domains & DomainSet.Air) != 0 && threat != null && threat.HasAirDefense;

        public static bool Suitable(UnitView u, OrderKind kind, DomainSet domains)
        {
            var dom = Model.Domains.Of(u.Role);
            if (dom == null || !Model.Domains.InMask(dom.Value, domains)) return false; // excludes missiles/buildings + off-domain
            switch (kind)
            {
                case OrderKind.Attack:
                    return u.Cap.CanEngageGround;
                case OrderKind.Defend:
                    return u.Cap.IsAirDefense || u.Cap.CanEngageGround;
                case OrderKind.Capture:
                    return u.Cap.CanCapture;   // manned, capture-capable ground/ships only
                case OrderKind.Resupply:
                    return u.Cap.IsSupply;     // supply trucks
                case OrderKind.Move:
                    return true;               // reposition any commandable unit in the chosen domains
                default:
                    return false;
            }
        }
    }
}
