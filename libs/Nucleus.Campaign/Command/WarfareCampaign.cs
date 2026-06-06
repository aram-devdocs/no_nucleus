using System.Collections.Generic;
using Nucleus.Core.Model;

namespace Nucleus.Core.Command
{
    /// <summary>
    /// The north-star campaign object: a persistent, two-faction dynamic war where BOTH sides run their own
    /// <see cref="CommanderBrain"/> over the same battlefield. Pure and engine-free — the caller supplies each
    /// faction's fog-of-war <see cref="WorldSnapshot"/> (built from the live game, or from the headless sim)
    /// and applies the returned tasks. This is the reusable substrate the <c>Nucleus.Warfare</c> mod and the
    /// "Nucleus Dynamic Warfare" mission drive; the dual-faction determinism + save/resume is proven headless.
    /// </summary>
    public sealed class WarfareCampaign
    {
        /// <summary>The two factions' commander states. Each is a full autonomous commander.</summary>
        public CommanderState Blufor { get; }
        public CommanderState Opfor { get; }

        /// <summary>How many <see cref="Step"/>s have run — the campaign clock, persisted across save/resume.</summary>
        public int Turn { get; set; }

        public WarfareCampaign(CommanderState blufor = null, CommanderState opfor = null)
        {
            Blufor = blufor ?? new CommanderState { Autonomy = AutonomyLevel.Auto };
            Opfor = opfor ?? new CommanderState { Autonomy = AutonomyLevel.Auto };
        }

        /// <summary>The per-faction tasking from one campaign step.</summary>
        public readonly struct StepResult
        {
            public readonly IReadOnlyList<UnitTask> Blufor;
            public readonly IReadOnlyList<UnitTask> Opfor;
            public StepResult(IReadOnlyList<UnitTask> blufor, IReadOnlyList<UnitTask> opfor)
            {
                Blufor = blufor;
                Opfor = opfor;
            }
        }

        /// <summary>
        /// Run one campaign tick: each faction's brain decides over its own view (its roster + the enemies it
        /// has detected) and returns the per-unit tasks the caller should execute. Deterministic — same states
        /// + same snapshots ⇒ same tasks. Advances <see cref="Turn"/>.
        /// </summary>
        public StepResult Step(WorldSnapshot bluforView, WorldSnapshot opforView)
        {
            var a = CommanderBrain.Tick(bluforView, Blufor);
            var b = CommanderBrain.Tick(opforView, Opfor);
            Turn++;
            return new StepResult(a, b);
        }
    }
}
