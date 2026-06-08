namespace Nucleus.Core.War
{
    /// <summary>Who runs a faction in the dynamic war — the human player, or the AI commander.</summary>
    public enum CommanderKind { Human, Ai }

    /// <summary>One side of the dynamic war: its attrition <see cref="WarScore"/>, spendable funds, and whether
    /// a human or the AI commands it (chosen per side, so you pick your level of interaction).</summary>
    public sealed class WarSide
    {
        public string FactionName;
        public CommanderKind Commander = CommanderKind.Ai;
        public float Funds;
        public readonly WarScore Score;

        public WarSide(string factionName, CommanderKind commander = CommanderKind.Ai, float startFunds = 5000f)
        {
            FactionName = factionName;
            Commander = commander;
            Funds = startFunds;
            Score = new WarScore();
        }

        /// <summary>Try to spend funds on reinforcement; debits both funds and attrition. False if unaffordable.</summary>
        public bool TrySpend(float cost)
        {
            if (cost <= 0f || cost > Funds) return false;
            Funds -= cost;
            Score.Spend(cost);
            return true;
        }
    }

    /// <summary>
    /// The dynamic-war scoreboard: two opposing sides, each with an attrition score + funds + commander kind.
    /// The win condition is attrition — a side is out when its score hits zero, and the war is over when one
    /// side is defeated. Pure and engine-free; the <c>Nucleus.Warfare</c> mod drives it from the live game and
    /// renders it; it persists with the campaign.
    /// </summary>
    public sealed class WarState
    {
        public WarSide Blufor { get; }
        public WarSide Opfor { get; }

        public WarState(WarSide blufor = null, WarSide opfor = null)
        {
            Blufor = blufor ?? new WarSide("Blufor");
            Opfor = opfor ?? new WarSide("Opfor");
        }

        /// <summary>True once either side has been attrited out.</summary>
        public bool IsOver => Blufor.Score.Defeated || Opfor.Score.Defeated;

        /// <summary>The winning side (the one NOT defeated), or null while the war continues / a double-out.</summary>
        public WarSide Winner =>
            Opfor.Score.Defeated && !Blufor.Score.Defeated ? Blufor
            : Blufor.Score.Defeated && !Opfor.Score.Defeated ? Opfor
            : null;
    }
}
