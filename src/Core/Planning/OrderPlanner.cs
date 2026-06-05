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
        public static IReadOnlyList<UnitView> SelectUnits(CommanderOrder order, IReadOnlyList<UnitView> roster,
            CommanderConfig cfg, ThreatPicture threat = null, IReadOnlyCollection<string> excludeIds = null)
        {
            float radius = order.Radius > 0f ? order.Radius : cfg.SelectionRadius;
            var inRange = roster
                .Where(u => u != null && !u.Disabled && u.Commandable)
                .Where(u => excludeIds == null || !excludeIds.Contains(u.Id)) // never poach units committed elsewhere
                .Where(u => Suitable(u, order.Kind, order.Domains))
                .Select(u => (u, dist: u.Position.HorizontalDistanceTo(order.Position)))
                .Where(x => x.dist <= radius);

            // Weapon-suitability: when attacking a position that holds armor, send our hardest-hitting
            // (highest anti-surface) units first; otherwise the nearest suitable units respond.
            bool preferPunch = order.Kind == OrderKind.Attack && threat != null && threat.HasArmor;
            var ordered = preferPunch
                ? inRange.OrderByDescending(x => x.u.AntiSurface).ThenBy(x => x.dist)
                : inRange.OrderBy(x => x.dist);

            // Force-sizing: take *enough*, not everyone. Attack/Defend scale to the known threat; other
            // kinds are bounded only by the suitable filter + the hard cap.
            return ordered.Take(RequiredForce(threat, order.Kind, cfg)).Select(x => x.u).ToList();
        }

        /// <summary>
        /// How many units an order should commit. Attack/Defend size to the known (fog-of-war) threat —
        /// outnumber it by the doctrine ratio, floored at MinForce — so we send a right-sized force, not the
        /// whole neighbourhood. Other kinds (Move/Capture/Resupply/Build) are threat-independent and bounded
        /// only by the suitable filter and the hard cap. Always ≤ MaxUnitsPerOrder.
        /// </summary>
        public static int RequiredForce(ThreatPicture threat, OrderKind kind, CommanderConfig cfg)
        {
            int max = cfg.MaxUnitsPerOrder;
            if (kind != OrderKind.Attack && kind != OrderKind.Defend) return max;
            int known = threat?.Count ?? 0;
            int want = (int)System.Math.Ceiling(known * cfg.ForceRatio);
            if (want < cfg.MinForce) want = cfg.MinForce;
            return want > max ? max : want;
        }

        public static TaskPlan Plan(CommanderOrder order, IReadOnlyList<UnitView> roster, ThreatPicture threat,
            CommanderConfig cfg, IReadOnlyCollection<string> excludeIds = null)
        {
            var selected = SelectUnits(order, roster, cfg, threat, excludeIds);
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
        public static AssignmentPreview Preview(CommanderOrder order, IReadOnlyList<UnitView> roster, ThreatPicture threat,
            CommanderConfig cfg, IReadOnlyCollection<string> excludeIds = null)
        {
            return new AssignmentPreview(SelectUnits(order, roster, cfg, threat, excludeIds), threat);
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
