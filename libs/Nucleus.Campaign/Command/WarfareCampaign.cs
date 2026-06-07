using System.Collections.Generic;
using Nucleus.Core.Model;
using Nucleus.Core.War;

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

        /// <summary>The attrition scoreboard — the win condition. Each side's score, funds, and commander kind.
        /// Falls as a side loses units/bases and spends on reinforcement; the war ends when one side hits zero.</summary>
        public WarState War { get; }

        /// <summary>How many <see cref="Step"/>s have run — the campaign clock, persisted across save/resume.</summary>
        public int Turn { get; set; }

        // Last observed roster size per side; a drop between ticks is attrition (units lost). -1 = uninitialized.
        private int _bluRoster = -1;
        private int _opRoster = -1;

        public WarfareCampaign(CommanderState blufor = null, CommanderState opfor = null, WarState war = null)
        {
            // Default: both sides full-AI (AiCreatesObjectives + AiAutoFill default true).
            Blufor = blufor ?? new CommanderState();
            Opfor = opfor ?? new CommanderState();
            War = war ?? new WarState();
        }

        /// <summary>The persisted roster baselines (so a resumed war doesn't mis-count the first tick as losses).</summary>
        public int BluforRosterBaseline { get => _bluRoster; set => _bluRoster = value; }
        public int OpforRosterBaseline { get => _opRoster; set => _opRoster = value; }

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
            ApplyAttrition(bluforView.Roster.Count, opforView.Roster.Count);
            var a = CommanderBrain.Tick(bluforView, Blufor);
            var b = CommanderBrain.Tick(opforView, Opfor);
            Turn++;
            return new StepResult(a, b);
        }

        // FALLBACK attrition: infer unit losses from roster shrinkage between ticks. This sees only the NET
        // change, so if a side both loses units and gains reinforcements the same tick it under-counts. The
        // live mission driver should instead feed exact kill events via RecordUnitLost (and toggle
        // UseRosterAttrition off); the heuristic exists for the headless sim, where no reinforcements arrive.
        private void ApplyAttrition(int bluNow, int opNow)
        {
            if (UseRosterAttrition)
            {
                if (_bluRoster >= 0 && bluNow < _bluRoster) War.Blufor.Score.UnitLost(_bluRoster - bluNow);
                if (_opRoster >= 0 && opNow < _opRoster) War.Opfor.Score.UnitLost(_opRoster - opNow);
            }
            _bluRoster = bluNow;
            _opRoster = opNow;
        }

        /// <summary>When true (default), <see cref="Step"/> infers unit losses from roster shrinkage. The live
        /// game driver sets this false and reports exact losses via <see cref="RecordUnitLost"/> instead, so a
        /// reinforcing side isn't under-bled.</summary>
        public bool UseRosterAttrition { get; set; } = true;

        /// <summary>Report exact unit losses for a side (from the game's kill/despawn events). Use this — not
        /// the roster heuristic — when reinforcements can arrive the same tick units die.</summary>
        public void RecordUnitLost(bool blufor, int count = 1)
            => (blufor ? War.Blufor : War.Opfor).Score.UnitLost(count);

        /// <summary>Record a base/airbase/HQ captured or destroyed for a side (drives the exponential spend falloff).</summary>
        public void RecordBaseLost(bool blufor, int count = 1)
            => (blufor ? War.Blufor : War.Opfor).Score.BaseLost(count);

        /// <summary>Spend a side's funds on reinforcement (off-map convoy/fleet, build at base). Debits funds AND
        /// attrition (falloff-weighted). Returns false if unaffordable.</summary>
        public bool Reinforce(bool blufor, float cost)
            => (blufor ? War.Blufor : War.Opfor).TrySpend(cost);

        /// <summary>True once a side is attrited out — the war is decided.</summary>
        public bool IsOver => War.IsOver;

        /// <summary>A flat, engine-free snapshot of the scoreboard for the HUD/UI to render.</summary>
        public readonly struct Scoreboard
        {
            public readonly string BluforName, OpforName;
            public readonly float BluforScore, OpforScore;
            public readonly float BluforFunds, OpforFunds;
            public readonly int BluforBasesLost, OpforBasesLost;
            public readonly int BluforUnitsLost, OpforUnitsLost;
            public readonly bool BluforAi, OpforAi;
            public readonly bool Over;
            public readonly string? WinnerName; // null while undecided

            public Scoreboard(WarState w, bool over, string? winner)
            {
                BluforName = w.Blufor.FactionName; OpforName = w.Opfor.FactionName;
                BluforScore = w.Blufor.Score.Score; OpforScore = w.Opfor.Score.Score;
                BluforFunds = w.Blufor.Funds; OpforFunds = w.Opfor.Funds;
                BluforBasesLost = w.Blufor.Score.BasesLost; OpforBasesLost = w.Opfor.Score.BasesLost;
                BluforUnitsLost = w.Blufor.Score.UnitsLost; OpforUnitsLost = w.Opfor.Score.UnitsLost;
                BluforAi = w.Blufor.Commander == CommanderKind.Ai; OpforAi = w.Opfor.Commander == CommanderKind.Ai;
                Over = over; WinnerName = winner;
            }
        }

        /// <summary>Build the current scoreboard read-model.</summary>
        public Scoreboard SnapshotBoard() => new Scoreboard(War, War.IsOver, War.Winner?.FactionName);
    }
}
