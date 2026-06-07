using System.Collections.Generic;
using System.Linq;
using Nucleus.Core.Model;

namespace Nucleus.Core.Planning
{
    /// <summary>
    /// Derives an order's battle-plan PHASE from its live state + the area threat — pure and testable, so
    /// the panel's plan view shows a meaningful "what is this order doing right now" label without the UI
    /// needing any game access. The manager fills OrderState.Phase from this each management tick.
    /// </summary>
    public static class BattlePlan
    {
        public static OrderPhase PhaseOf(OrderState s, ThreatPicture threat,
            IReadOnlyDictionary<string, Vec3> posById, float arriveRadius)
        {
            if (s.Status == OrderStatus.Failed) return OrderPhase.Failed;
            if (s.Status == OrderStatus.Complete) return OrderPhase.Complete;

            var o = s.Order;
            if (o.Kind == OrderKind.Build) return OrderPhase.Queued;

            bool air = (o.Domains & DomainSet.Air) != 0;

            // SEAD-before-strike: air order while enemy air defenses remain -> suppressing.
            if (air && threat != null && threat.HasAirDefense) return OrderPhase.Suppressing;

            bool hasUnits = s.AssignedUnitIds.Count > 0;
            if (!hasUnits) return air ? OrderPhase.AirTasking : OrderPhase.Forming;

            bool anyEnRoute = s.AssignedUnitIds.Any(id =>
                posById != null && posById.TryGetValue(id, out var p)
                && p.HorizontalDistanceTo(o.Position) > arriveRadius);

            if (anyEnRoute) return OrderPhase.Advancing;
            if (threat != null && threat.Count > 0) return OrderPhase.Engaging; // arrived, enemies present
            return OrderPhase.Holding;                                          // arrived, area quiet
        }
    }
}
