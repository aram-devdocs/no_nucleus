using System.Collections.Generic;
using System.Linq;
using Nucleus.Core.Model;

namespace Nucleus.Core.Command
{
    /// <summary>
    /// A buyable production package: one purchase delivers a fixed <see cref="Composition"/> of role
    /// families at a fixed cost. The atom the production planner shops from. Effectively-immutable holder
    /// (get-only) over a mutable Composition; engine-free.
    /// </summary>
    public sealed class ConvoyOption
    {
        public string Name { get; }
        public float Cost { get; }

        /// <summary>The role families + counts this convoy provides when it arrives.</summary>
        public Composition Delivers { get; }

        /// <summary>Human-readable real contents for display, e.g. "3× MBT, 1× SAM" (from the game's convoy
        /// constituents). Empty when unknown.</summary>
        public string Contents { get; }

        /// <summary>Where this package actually enters the world when bought — the real game spawn point: an
        /// off-map edge/depot for ground convoys, the airbase for aircraft, a port/ship for naval. Zero (origin)
        /// when the game can't tell us (no base/depot yet); the map layer can skip drawing an arrival marker then.</summary>
        public Vec3 SpawnPoint { get; }

        public ConvoyOption(string name, float cost, Composition delivers, string contents = "", Vec3 spawnPoint = default)
        {
            Name = name;
            Cost = cost;
            Delivers = delivers ?? new Composition();
            Contents = contents ?? "";
            SpawnPoint = spawnPoint;
        }

        public override string ToString() => $"{Name} ({Cost:0}) -> {Delivers}";
    }

    /// <summary>
    /// The menu of convoy packages a commander may buy — what production "shops" from to fill a gap.
    /// Immutable snapshot of the options. Pure, Unity-free.
    /// </summary>
    public sealed class ConvoyCatalog
    {
        public IReadOnlyList<ConvoyOption> Options { get; }

        public ConvoyCatalog(IEnumerable<ConvoyOption> options)
        {
            Options = (options ?? Enumerable.Empty<ConvoyOption>())
                .Where(o => o != null)
                .ToList();
        }
    }
}
