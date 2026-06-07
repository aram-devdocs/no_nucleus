using System.Collections.Generic;
using System.Linq;
using Nucleus.Core.Model;

namespace Nucleus.Core.Command
{
    /// <summary>A spatial cluster of known enemies — the pocket the planner tasks an objective against, instead
    /// of individual contacts. Carries footprint, aggregate threat, summed priority and dominant family.</summary>
    public sealed class ThreatGroup
    {
        public Vec3 Center { get; }
        /// <summary>Largest distance from <see cref="Center"/> to any member — the footprint.</summary>
        public float Radius { get; }
        public ThreatPicture Threat { get; }
        /// <summary>Sum of members' <see cref="EnemyView.StrategicPriority"/> — how much this pocket matters.</summary>
        public float TotalStrategicPriority { get; }
        public RoleFamily Dominant { get; }
        public int Count { get; }
        /// <summary>Low-confidence members — scout before committing a strike force.</summary>
        public int InaccurateCount { get; }
        public IReadOnlyList<EnemyView> Members { get; }

        public ThreatGroup(IReadOnlyList<EnemyView> members)
        {
            var live = (members ?? new List<EnemyView>()).Where(m => m != null).ToList();
            if (live.Count == 0)
                throw new System.ArgumentException("ThreatGroup requires at least one non-null member.", nameof(members));
            Members = live;
            Count = live.Count;

            float sx = 0f, sy = 0f, sz = 0f, priority = 0f;
            int inaccurate = 0;
            foreach (var e in live)
            {
                sx += e.Position.X;
                sy += e.Position.Y;
                sz += e.Position.Z;
                priority += e.StrategicPriority;
                if (!e.Accurate) inaccurate++;
            }
            InaccurateCount = inaccurate;
            float inv = 1f / Count;
            Center = new Vec3(sx * inv, sy * inv, sz * inv);
            TotalStrategicPriority = priority;

            float radius = 0f;
            foreach (var e in live)
            {
                float d = Center.HorizontalDistanceTo(e.Position);
                if (d > radius) radius = d;
            }
            Radius = radius;

            Threat = new ThreatPicture(live);

            Dominant = live
                .GroupBy(e => Families.Of(e.Cap.Role))
                .OrderByDescending(g => g.Count())
                .ThenBy(g => (int)g.Key)
                .Select(g => g.Key)
                .FirstOrDefault();
        }
    }

    /// <summary>Greedy proximity clustering of known enemies into <see cref="ThreatGroup"/>s. Highest-priority
    /// contacts seed first (Id tie-break) so the board is stable across ticks.</summary>
    public static class ThreatBoard
    {
        public static IReadOnlyList<ThreatGroup> Build(IReadOnlyList<EnemyView> known, float clusterRadius)
        {
            var groups = new List<ThreatGroup>();
            if (known == null) return groups;

            var remaining = known
                .Where(e => e != null)
                .OrderByDescending(e => e.StrategicPriority)
                .ThenBy(e => e.Id)
                .ToList();

            var taken = new bool[remaining.Count];
            for (int i = 0; i < remaining.Count; i++)
            {
                if (taken[i]) continue;
                var seed = remaining[i];
                taken[i] = true;

                var members = new List<EnemyView> { seed };
                for (int j = i + 1; j < remaining.Count; j++)
                {
                    if (taken[j]) continue;
                    if (seed.Position.HorizontalDistanceTo(remaining[j].Position) <= clusterRadius)
                    {
                        taken[j] = true;
                        members.Add(remaining[j]);
                    }
                }

                groups.Add(new ThreatGroup(members));
            }

            return groups;
        }
    }
}
