using NuclearOption.Networking;

namespace CommanderLayer.Game
{
    /// <summary>
    /// Commission-only production (the user's "keep it real"): queue the cheapest affordable convoy at the
    /// faction's normal spawn — no teleport, no auto-rally. Host-side ServerRpc.
    /// </summary>
    public sealed class GameProduction
    {
        public string Commission()
        {
            if (!GameManager.GetLocalPlayer<Player>(out var player) || player == null) return "no local player";
            if (!GameManager.GetLocalFaction(out var faction) || faction == null) return "no faction";
            if (!GameManager.GetLocalHQ(out var hq) || hq == null) return "no HQ";

            float funds = hq.factionFunds;
            Faction.ConvoyGroup best = null;
            float bestCost = float.MaxValue;
            var groups = faction.GetConvoyGroups();
            if (groups != null)
            {
                foreach (var g in groups)
                {
                    if (g == null) continue;
                    float c = g.GetCost();
                    if (c <= funds && c < bestCost) { best = g; bestCost = c; }
                }
            }
            if (best == null) return $"no affordable convoy (funds {funds:0})";

            player.CmdPurchaseConvoy(best.Name);
            return $"queued {best.Name} (cost {bestCost:0}, funds {funds:0})";
        }
    }
}
