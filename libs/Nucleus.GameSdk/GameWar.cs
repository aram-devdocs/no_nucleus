using System.Collections.Generic;
using System.Linq;
using Nucleus.Core.War;

namespace Nucleus.Game
{
    /// <summary>
    /// Captures the live battlefield census — per faction, how many units are still alive and how many airbases
    /// it holds — from the game registries (<c>UnitRegistry.allUnits</c> + <c>FactionRegistry.airbaseLookup</c>,
    /// both public statics). The Warfare mod diffs this tick-over-tick to drive attrition. Exception-safe and
    /// null-guarded like <see cref="GameRoster"/>; a missing registry maps to an empty census (attrition simply
    /// doesn't advance) rather than throwing.
    /// </summary>
    public sealed class GameWar
    {
        private static readonly IReadOnlyList<FactionCensus> Empty = new List<FactionCensus>();

        public IReadOnlyList<FactionCensus> Census()
        {
            try { return CensusUnsafe(); }
            catch { return Empty; } // live registries can mutate mid-enumeration; never throw out of the mod tick
        }

        private IReadOnlyList<FactionCensus> CensusUnsafe()
        {
            var units = new Dictionary<string, int>();
            var bases = new Dictionary<string, int>();

            var all = UnitRegistry.allUnits;
            if (all == null) return Empty;

            // Snapshot to an array so a unit (de)spawning mid-tick can't throw "collection was modified".
            var snapshot = all.ToArray();
            foreach (var u in snapshot)
            {
                if (u == null || u.disabled) continue;
                if (u.unitState != Unit.UnitState.Active) continue;
                var name = u.NetworkHQ?.faction?.factionName;
                if (string.IsNullOrEmpty(name)) continue;
                units.TryGetValue(name, out var c);
                units[name] = c + 1;
            }

            var airbases = FactionRegistry.airbaseLookup;
            if (airbases != null)
            {
                foreach (var ab in airbases.Values)
                {
                    if (ab == null || ab.disabled) continue;
                    var name = ab.CurrentHQ?.faction?.factionName;
                    if (string.IsNullOrEmpty(name)) continue;
                    bases.TryGetValue(name, out var c);
                    bases[name] = c + 1;
                }
            }

            var result = new List<FactionCensus>();
            var names = new HashSet<string>(units.Keys);
            names.UnionWith(bases.Keys);
            foreach (var name in names)
            {
                units.TryGetValue(name, out var u);
                bases.TryGetValue(name, out var b);
                result.Add(new FactionCensus(name, u, b));
            }
            return result;
        }
    }
}
