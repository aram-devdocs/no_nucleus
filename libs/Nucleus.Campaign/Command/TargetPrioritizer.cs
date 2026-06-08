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
                // AirGroundPref nudges an air-first commander toward enemy aircraft pockets (neutral at 0.5).
                if (group.Dominant == RoleFamily.AirCombat)
                    threatBump += ((doctrine?.AirPreference ?? 0.5f) - 0.5f);

                float score = strategic + proximity + threatBump;
                ranked.Add(new ScoredTarget(group, score, SuggestKind(group, doctrine)));
            }

            return ranked
                .OrderByDescending(t => t.Score)
                .ThenBy(t => t.Group.Center.X)
                .ThenBy(t => t.Group.Count)
                .ToList();
        }

        // Aircraft pocket -> contest the air; a pocket we can't read -> scout it (ReconBias widens that to
        // mostly-fuzzy pockets); holdable ground (armor/infantry) -> Capture; everything else -> Destroy.
        private static ObjectiveKind SuggestKind(ThreatGroup group, Doctrine doctrine)
        {
            if (group.Dominant == RoleFamily.AirCombat) return ObjectiveKind.ControlAirspace;

            bool allFuzzy = group.InaccurateCount >= group.Count;
            bool mostlyFuzzy = group.InaccurateCount * 2 >= group.Count;
            float reconWeight = doctrine?.ReconWeight ?? 1.0f;
            if (allFuzzy || (mostlyFuzzy && reconWeight >= 1.2f)) return ObjectiveKind.Recon;

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
