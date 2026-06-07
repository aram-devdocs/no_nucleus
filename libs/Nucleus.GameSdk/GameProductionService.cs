using System.Collections.Generic;
using Nucleus.Core.Command;
using NuclearOption.Networking;

namespace Nucleus.Game
{
    /// <summary>
    /// Game adapter that turns the pure Core production plan into real convoy purchases. Reads the local
    /// faction's convoy groups to build a <see cref="ConvoyCatalog"/>, and drains a <see cref="ProductionQueue"/>
    /// into <see cref="Player.CmdPurchaseConvoy"/> calls as long as funds allow. Null-safe: with no local
    /// player/faction/HQ it degrades to an empty catalog / no-op drain.
    /// </summary>
    public sealed class GameProductionService
    {
        /// <summary>
        /// Snapshot the local faction's buyable convoys as a <see cref="ConvoyCatalog"/>. The delivered
        /// <see cref="Composition"/> is derived from a name heuristic (see DeliversFor) because the convoy's
        /// unit contents aren't reliably readable from the public game API. Empty catalog when no local faction.
        /// </summary>
        public ConvoyCatalog Catalog()
        {
            var options = new List<ConvoyOption>();
            if (!GameManager.GetLocalFaction(out var faction) || faction == null)
                return new ConvoyCatalog(options);

            var groups = faction.GetConvoyGroups();
            if (groups != null)
            {
                foreach (var g in groups)
                {
                    if (g == null) continue;
                    options.Add(new ConvoyOption(g.Name, SafeCost(g), DeliversFor(g.Name), ContentsOf(g)));
                }
            }
            return new ConvoyCatalog(options);
        }

        /// <summary>
        /// Pay down the production queue: while the head request is affordable (Cost &lt;= current faction
        /// funds) dequeue it and purchase the convoy; stop at the first unaffordable request or when empty.
        /// Null-safe — does nothing without a local player/faction/HQ.
        /// </summary>
        private float _lastPurchase = -1000f; // timeSinceLevelLoad of our last buy (game enforces a 60s cooldown)

        /// <summary>Drain one affordable queued purchase (game caps convoy buys to 1/60s). Returns the dispatched
        /// request so the caller can announce it on the battle feed, or null when nothing was bought this tick.</summary>
        public PurchaseRequest Drain(ProductionQueue queue)
        {
            if (queue == null || queue.Pending.Count == 0) return null;
            if (!GameManager.GetLocalPlayer<Player>(out var player) || player == null) return null;
            if (!GameManager.GetLocalFaction(out var faction) || faction == null) return null;
            if (!GameManager.GetLocalHQ(out var hq) || hq == null) return null;

            // The game allows at most ONE convoy purchase per 60s (Player.CmdPurchaseConvoy); buying more in a
            // burst silently no-ops. So drain a SINGLE affordable request per cooldown — never loop-dequeue.
            if (UnityEngine.Time.timeSinceLevelLoad < _lastPurchase + 60f) return null;

            var req = queue.Pending[0];
            if (req == null) { queue.Dequeue(); return null; }
            if (req.Cost > hq.factionFunds) return null; // can't afford the head yet — leave it queued

            queue.Dequeue();
            player.CmdPurchaseConvoy(req.ConvoyName);
            _lastPurchase = UnityEngine.Time.timeSinceLevelLoad;
            Nucleus.Core.NucleusLog.Info($"Production purchase: {req.ConvoyName} (cost {req.Cost:0}, funds {hq.factionFunds:0})");
            return req;
        }

        /// <summary>Cost of a convoy computed defensively from its constituents (the game's GetCost throws on a
        /// null constituent Type). Mirrors the null-guarding in ContentsOf. 0 if unreadable.</summary>
        private static float SafeCost(Faction.ConvoyGroup g)
        {
            if (g?.Constituents == null) return 0f;
            float cost = 0f;
            foreach (var c in g.Constituents)
                if (c?.Type != null) cost += c.Type.value * c.Count;
            return cost;
        }

        /// <summary>Real, human-readable contents of a convoy from the game's own data
        /// (<c>ConvoyGroup.Constituents</c> → unit name × count), e.g. "3× MBT, 1× SAM". Empty if unreadable.</summary>
        private static string ContentsOf(Faction.ConvoyGroup g)
        {
            if (g?.Constituents == null || g.Constituents.Count == 0) return "";
            var parts = new List<string>();
            foreach (var c in g.Constituents)
            {
                if (c?.Type == null) continue;
                string nm = !string.IsNullOrEmpty(c.Type.unitName) ? c.Type.unitName : c.Type.name;
                parts.Add(c.Count > 1 ? $"{c.Count}× {nm}" : nm);
            }
            return string.Join(", ", parts);
        }

        /// <summary>
        /// Name-heuristic mapping a convoy's display name to the role families it delivers. The convoy's real
        /// contents are unknown/S0-gated, so we infer from keywords; each matched keyword adds one of that
        /// family. A name with no keyword falls back to a single Armor (a safe, squadable default). One table
        /// so it's easy to tune as more convoy names are observed in-game.
        /// </summary>
        private static Core.Command.Composition DeliversFor(string name)
        {
            var delivers = new Core.Command.Composition();
            string n = (name ?? string.Empty).ToLowerInvariant();

            if (n.Contains("armor") || n.Contains("tank") || n.Contains("mbt") || n.Contains("afv"))
                delivers.Add(RoleFamily.Armor);
            if (n.Contains("sam") || n.Contains("aa") || n.Contains("air defen") || n.Contains("flak"))
                delivers.Add(RoleFamily.AirDefense);
            if (n.Contains("artillery") || n.Contains("howitzer") || n.Contains("mlrs"))
                delivers.Add(RoleFamily.Artillery);
            if (n.Contains("supply") || n.Contains("truck") || n.Contains("logistic") || n.Contains("fuel") || n.Contains("ammo"))
                delivers.Add(RoleFamily.Supply);
            if (n.Contains("radar") || n.Contains("awacs"))
                delivers.Add(RoleFamily.Recon);
            if (n.Contains("infantry") || n.Contains("troop"))
                delivers.Add(RoleFamily.Infantry);

            if (delivers.Total == 0) delivers.Add(RoleFamily.Armor); // safe default: one Armor
            return delivers;
        }
    }
}
