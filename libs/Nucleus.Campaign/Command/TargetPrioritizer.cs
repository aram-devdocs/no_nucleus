using System.Collections.Generic;
using System.Linq;
using Nucleus.Core.Model;

namespace Nucleus.Core.Command
{
    /// <summary>A <see cref="ThreatGroup"/> with its computed <see cref="Score"/> and the
    /// <see cref="ObjectiveKind"/> to pursue against it — the output unit of <see cref="TargetPrioritizer"/>.</summary>
    public sealed class ScoredTarget
    {
        public ThreatGroup Group { get; }
        public float Score { get; }
        public ObjectiveKind SuggestedKind { get; }

        public ScoredTarget(ThreatGroup group, float score, ObjectiveKind suggestedKind)
        {
            Group = group;
            Score = score;
            SuggestedKind = suggestedKind;
        }
    }

    /// <summary>Ranks the threat board into an ordered target list, scoring each pocket on strategic priority,
    /// proximity to home, and an air-defense/radar bump. <see cref="Doctrine.RiskTolerance"/> shifts the balance:
    /// aggressive favors high-value/distant, cautious favors near/low-risk.</summary>
    public static class TargetPrioritizer
    {
        // Proximity falls off over ~10km; ProximityWeight is its full-strength contribution at zero range.
        private const float ProximityFalloff = 10000f;

        public static IReadOnlyList<ScoredTarget> Rank(
            IReadOnlyList<ThreatGroup> groups, Vec3 homeBase, Doctrine doctrine)
        {
            var ranked = new List<ScoredTarget>();
            if (groups == null) return ranked;

            float risk = doctrine != null ? doctrine.RiskTolerance : 0.5f;
            if (risk < 0f) risk = 0f;
            else if (risk > 1f) risk = 1f;

            float priorityWeight = 1.0f + risk;                 // 1.0 cautious .. 2.0 aggressive
            float proximityWeight = 1.5f - risk;                // 1.5 cautious .. 0.5 aggressive

            foreach (var group in groups)
            {
                if (group == null) continue;

                float strategic = priorityWeight * group.TotalStrategicPriority;

                float distance = homeBase.HorizontalDistanceTo(group.Center);
                float proximity = proximityWeight / (1f + distance / ProximityFalloff);

                // Air defense / radar pockets are worth hitting first — clearing them unlocks air ops.
                float threatBump = 0f;
                if (group.Threat != null)
                {
                    if (group.Threat.HasAirDefense) threatBump += 0.5f;
                    if (group.Threat.HasRadar) threatBump += 0.25f;
                }

                float score = strategic + proximity + threatBump;
                ranked.Add(new ScoredTarget(group, score, SuggestKind(group)));
            }

            return ranked
                .OrderByDescending(t => t.Score)
                .ThenBy(t => t.Group.Center.X)
                .ThenBy(t => t.Group.Count)
                .ToList();
        }

        // Holdable ground (armor/infantry) -> Capture; everything else -> Destroy. Recon-on-low-confidence is
        // left to the roster-aware pass (a pure-accuracy rule starves a force with no scout units).
        private static ObjectiveKind SuggestKind(ThreatGroup group)
        {
            switch (group.Dominant)
            {
                case RoleFamily.Armor:
                case RoleFamily.Infantry:
                    return ObjectiveKind.CapturePoint;
                default:
                    return ObjectiveKind.DestroyTarget;
            }
        }
    }
}
