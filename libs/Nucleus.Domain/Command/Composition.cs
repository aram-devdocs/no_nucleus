using System.Collections.Generic;
using System.Linq;

namespace Nucleus.Core.Command
{
    /// <summary>
    /// A force requirement / make-up expressed as counts per role family — e.g. "2× Armor, 1× AirDefense".
    /// Used as a squad's target make-up and as the gap data Production fills + displays. Mutable model
    /// (Add/Set); engine-free.
    /// </summary>
    public sealed class Composition
    {
        private readonly Dictionary<RoleFamily, int> _counts = new Dictionary<RoleFamily, int>();

        public int Get(RoleFamily f) => _counts.TryGetValue(f, out var n) ? n : 0;
        public void Set(RoleFamily f, int n) { if (n <= 0) _counts.Remove(f); else _counts[f] = n; }
        public void Add(RoleFamily f, int n = 1) => Set(f, Get(f) + n);

        public int Total => _counts.Values.Sum();
        public IEnumerable<KeyValuePair<RoleFamily, int>> Items => _counts;

        /// <summary>The shortfall of this (need) versus a supplied make-up — positive counts only.</summary>
        public Composition Shortfall(Composition have)
        {
            var gap = new Composition();
            foreach (var kv in _counts)
            {
                int missing = kv.Value - (have?.Get(kv.Key) ?? 0);
                if (missing > 0) gap.Set(kv.Key, missing);
            }
            return gap;
        }

        public override string ToString() =>
            _counts.Count == 0 ? "(none)" : string.Join(", ", _counts.OrderBy(k => k.Key).Select(k => $"{k.Value}× {k.Key}"));
    }
}
