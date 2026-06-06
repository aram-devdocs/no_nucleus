using System.Collections.Generic;
using CommanderLayer.Abstractions;
using CommanderLayer.Core.Model;
using CommanderLayer.Core.Ports;
using CommanderLayer.Game;

namespace CommanderLayer.Host
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

        public IReadOnlyList<UnitView> Roster() => _roster.BuildRoster();
        public IReadOnlyList<EnemyView> KnownEnemiesNear(Vec3 center, float radius) => _intel.KnownEnemiesNear(center, radius);
        public void Execute(UnitTask task) => _cmds.Execute(task);
        public float Funds() => GameManager.GetLocalHQ(out var hq) && hq != null ? hq.factionFunds : 0f;
        public bool TryGetLocalFaction(out FactionInfo faction) => _player.TryGetLocalFaction(out faction);
        public IMapProjection MapProjection => _projection;
    }
}
