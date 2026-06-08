using System.Collections.Generic;
using Nucleus.Core.Command;
using Nucleus.Core.Model;
using NuclearOption.Networking;

namespace Nucleus.Game
{
    /// <summary>Game adapter turning the pure Core production plan into real convoy purchases. Null-safe: with
    /// no local player/faction/HQ it degrades to an empty catalog / no-op drain.</summary>
    public sealed class GameProductionService
    {
        /// <summary>The game's convoy-purchase cooldown — at most one <see cref="Player.CmdPurchaseConvoy"/> per
        /// 60s (FactionHQ.CmdGetDelaySpawnConvoy mirrors this window). Drives both the drain gate and the
        /// per-queue-item progress/ETA the UI shows.</summary>
        public const float PurchaseCooldownSeconds = 60f;

        public ConvoyCatalog Catalog()
        {
            var options = new List<ConvoyOption>();
            if (!GameManager.GetLocalFaction(out var faction) || faction == null)
                return new ConvoyCatalog(options);

            // HQ is optional — without it we still list the menu, just with no real spawn point (origin).
            FactionHQ hq = GameManager.GetLocalHQ(out var localHq) ? localHq : null;

            var groups = faction.GetConvoyGroups();
            if (groups != null)
            {
                foreach (var g in groups)
                {
                    if (g == null) continue;
                    var delivers = DeliversFor(g.Name);
                    options.Add(new ConvoyOption(g.Name, SafeCost(g), delivers, ContentsOf(g), SpawnPointFor(hq, delivers)));
                }
            }
            return new ConvoyCatalog(options);
        }

        /// <summary>Total funds actually committed to dispatched purchases — the real reinforcement spend the war
        /// score should be debited by (via <see cref="OnSpend"/>).</summary>
        public float TotalSpent { get; private set; }

        /// <summary>Raised with the REAL cost each time a purchase is dispatched, so the host can feed it into the
        /// war score (WarScore.Spend / WarfareCampaign.Reinforce). Null = no scoreboard wired (no-op).</summary>
        public System.Action<float> OnSpend;

        private float _lastPurchase = -1000f; // timeSinceLevelLoad of last buy (game enforces a 60s cooldown)

        /// <summary>Live delivery view of the queue under the game's one-buy-per-60s cooldown: progress bar +
        /// countdown per pending item, computed from the REAL last-purchase time and the real cooldown. Pure
        /// passthrough to <see cref="ProductionQueue.Snapshot"/> with the game's clock.</summary>
        public IReadOnlyList<QueueItemView> QueueSnapshot(ProductionQueue queue)
        {
            if (queue == null) return System.Array.Empty<QueueItemView>();
            return queue.Snapshot(UnityEngine.Time.timeSinceLevelLoad, _lastPurchase, PurchaseCooldownSeconds);
        }

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
            if (UnityEngine.Time.timeSinceLevelLoad < _lastPurchase + PurchaseCooldownSeconds) return null;

            var req = queue.Pending[0];
            if (req == null) { queue.Dequeue(); return null; }
            if (req.Cost > hq.factionFunds) return null; // can't afford the head yet — leave it queued

            queue.Dequeue();
            player.CmdPurchaseConvoy(req.ConvoyName);
            _lastPurchase = UnityEngine.Time.timeSinceLevelLoad;
            TotalSpent += req.Cost;          // real reinforcement spend …
            OnSpend?.Invoke(req.Cost);       // … fed to the war score by the host (no-op if unwired)
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
            // Aircraft packages — distinct keywords that don't collide with "air defen" (AirDefense, above).
            if (n.Contains("aircraft") || n.Contains("fighter") || n.Contains("jet") || n.Contains("interceptor")
                || n.Contains("bomber") || n.Contains("sead") || n.Contains("squadron") || n.Contains("air wing")
                || n.Contains("airwing") || n.Contains("helicopter") || n.Contains("helo") || n.Contains("gunship")
                || n.Contains("strike air") || n.Contains("air strike") || n.Contains("airstrike"))
                delivers.Add(RoleFamily.AirCombat);
            // Naval packages — ships/fleets delivered by sea.
            if (n.Contains("naval") || n.Contains("ship") || n.Contains("fleet") || n.Contains("carrier")
                || n.Contains("frigate") || n.Contains("destroyer") || n.Contains("corvette") || n.Contains("cruiser")
                || n.Contains("warship") || n.Contains("gunboat") || n.Contains("patrol boat"))
                delivers.Add(RoleFamily.Naval);

            if (delivers.Total == 0) delivers.Add(RoleFamily.Armor); // safe default: one Armor
            return delivers;
        }

        /// <summary>The package's REAL entry point pulled from the game: aircraft arrive at the faction's airbase,
        /// ships at the nearest friendly ship (port proxy), and ground convoys at the nearest vehicle depot
        /// (the off-map-edge resupply point). Origin (zero) when no base/depot exists yet — the map layer treats
        /// that as "no arrival marker". The faction's first airbase anchors the "nearest" lookups (its home area).</summary>
        private static Vec3 SpawnPointFor(FactionHQ hq, Composition delivers)
        {
            if (hq == null) return default;

            Airbase firstBase = null;
            foreach (var ab in hq.GetAirbases()) { if (ab != null) { firstBase = ab; break; } }
            UnityEngine.Vector3 home = (firstBase != null && firstBase.center != null)
                ? firstBase.center.position : UnityEngine.Vector3.zero;

            // Aircraft spawn at the airbase itself.
            if (delivers.Get(RoleFamily.AirCombat) > 0 && firstBase != null && firstBase.center != null)
                return GameConvert.ToVec3(firstBase.center.GlobalPosition());

            // Ships arrive by sea — anchor on the nearest friendly ship (a port/fleet proxy) when one exists.
            if (delivers.Get(RoleFamily.Naval) > 0
                && hq.TryGetNearestShip(home.ToGlobalPosition(), out var ship, out _) && ship != null)
                return GameConvert.ToVec3(ship.GlobalPosition());

            // Ground convoys are deployed from the nearest vehicle depot (the resupply edge).
            var depot = hq.GetNearestDepot(home);
            if (depot != null && depot.transform != null)
                return GameConvert.ToVec3(depot.transform.GlobalPosition());

            // No depot/ship: fall back to the home airbase area so the marker still reads as "from base".
            return firstBase != null ? GameConvert.ToVec3(home.ToGlobalPosition()) : default;
        }
    }
}
