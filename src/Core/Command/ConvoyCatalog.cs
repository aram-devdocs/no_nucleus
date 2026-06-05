using System.Collections.Generic;
using System.Linq;

namespace CommanderLayer.Core.Command
{
    /// <summary>
    /// A buyable production package: one purchase delivers a fixed <see cref="Composition"/> of role
    /// families at a fixed cost. The atom the production planner shops from. Pure value type.
    /// </summary>
    public sealed class ConvoyOption
    {
        public string Name { get; }
        public float Cost { get; }

        /// <summary>The role families + counts this convoy provides when it arrives.</summary>
        public Composition Delivers { get; }

        public ConvoyOption(string name, float cost, Composition delivers)
        {
            Name = name;
            Cost = cost;
            Delivers = delivers ?? new Composition();
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
