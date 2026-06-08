using System.Collections.Generic;
using Nucleus.Abstractions;
using Nucleus.Core.Model;
using Nucleus.Core.Ports;
using Nucleus.Game;
using NuclearOption.Networking;

namespace Nucleus.Host
{
    /// <summary>
    /// The single shared read/command surface over the live game, owned by the host and injected into every
    /// mod via <see cref="IModContext"/> — so the plugins don't each enumerate the roster or build their own
    /// intel. Thin wrapper over the GameSdk adapters. (Implements the Abstractions contract, so it lives in
    /// the app, not GameSdk — GameSdk must not reference Abstractions.)
    /// </summary>
    public sealed class GameServices : IGameServices
    {
        private readonly GameRoster _roster = new GameRoster();
        private readonly GameIntel _intel = new GameIntel();
        private readonly GameUnitCommands _cmds = new GameUnitCommands();
        private readonly GamePlayerContext _player = new GamePlayerContext();
        private readonly DynamicMapProjection _projection = new DynamicMapProjection();
        private readonly GameWar _war = new GameWar();

        public IReadOnlyList<UnitView> Roster() => _roster.BuildRoster();
        public IReadOnlyList<EnemyView> KnownEnemiesNear(Vec3 center, float radius) => _intel.KnownEnemiesNear(center, radius);
        public void Execute(UnitTask task) => _cmds.Execute(task);
        public float Funds() => GameManager.GetLocalHQ(out var hq) && hq != null ? hq.factionFunds : 0f;
        public bool TryGetLocalFaction(out FactionInfo faction) => _player.TryGetLocalFaction(out faction);
        public IMapProjection MapProjection => _projection;
        public IReadOnlyList<Nucleus.Core.War.FactionCensus> WarCensus() => _war.Census();

        public IReadOnlyList<string> FactionNames()
        {
            var list = new List<string>();
            try
            {
                foreach (var f in FactionRegistry.factions)
                    if (f != null && !string.IsNullOrEmpty(f.factionName)) list.Add(f.factionName);
            }
            catch { /* registry not ready */ }
            return list;
        }

        public bool HasLocalFaction
        {
            get { try { return GameManager.GetLocalHQ(out var hq) && hq != null; } catch { return false; } }
        }

        public bool JoinFaction(string factionName)
        {
            try
            {
                if (!GameManager.GetLocalPlayer<Player>(out var player) || player == null) return false;
                var hq = FactionRegistry.HqFromName(factionName);
                if (hq == null) return false;
                player.SetFaction(hq);
                var map = SceneSingleton<DynamicMap>.i;
                if (map != null) { map.SetFaction(hq); map.Maximize(); }
                return true;
            }
            catch { return false; }
        }

        public string CurrentMissionName
        {
            get { try { return MissionManager.CurrentMission?.Name; } catch { return null; } }
        }

        private static readonly IReadOnlyList<UnitView> EmptyRoster = new List<UnitView>();
        private static readonly IReadOnlyList<EnemyView> EmptyEnemies = new List<EnemyView>();

        public IReadOnlyList<UnitView> RosterFor(string factionName)
        {
            try { var hq = FactionRegistry.HqFromName(factionName); return hq != null ? _roster.BuildRosterFor(hq) : EmptyRoster; }
            catch { return EmptyRoster; }
        }

        public IReadOnlyList<EnemyView> KnownEnemiesFor(string factionName, Vec3 center, float radius)
        {
            try { var hq = FactionRegistry.HqFromName(factionName); return hq != null ? _intel.KnownEnemiesNearFor(hq, center, radius) : EmptyEnemies; }
            catch { return EmptyEnemies; }
        }
    }
}
