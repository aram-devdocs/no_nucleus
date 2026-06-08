namespace Nucleus.Core.War
{
    /// <summary>A point-in-time count of one faction's live forces on the battlefield: its alive units and the
    /// airbases it currently holds. The Game layer captures this from the live game each tick; the Warfare mod
    /// diffs it tick-over-tick and feeds the drops into the attrition <see cref="WarScore"/>. Pure data.</summary>
    public readonly struct FactionCensus
    {
        public readonly string FactionName;
        public readonly int AliveUnits;
        public readonly int Airbases;

        public FactionCensus(string factionName, int aliveUnits, int airbases)
        {
            FactionName = factionName;
            AliveUnits = aliveUnits;
            Airbases = airbases;
        }
    }
}
