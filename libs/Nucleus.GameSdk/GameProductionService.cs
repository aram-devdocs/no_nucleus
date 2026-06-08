using System.Collections.Generic;
using Nucleus.Core.Command;
using NuclearOption.Networking;

namespace Nucleus.Game
{
    /// <summary>Game adapter turning the pure Core production plan into real convoy purchases. Null-safe: with
    /// no local player/faction/HQ it degrades to an empty catalog / no-op drain.</summary>
    public sealed class GameProductionService
    {
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

        private float _lastPurchase = -1000f; // timeSinceLevelLoad of last buy (game enforces a 60s cooldown)

        /// <summary>Drain one affordable queued purchase (game caps convoy buys to 1/60s). Returns the dispatched
        /// request to announce on the feed, or null when nothing was bought.</summary>
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

        /// <summary>Convoy contents from the game's own data, e.g. "3× MBT, 1× SAM". Empty if unreadable.</summary>
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

        // Infer delivered families from name keywords (real contents aren't reliably readable from the API);
        // no keyword falls back to a single Armor.
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
