using System.Collections.Generic;
using CommanderLayer.Core.Model;

namespace CommanderLayer.Core.Command
{
    /// <summary>
    /// The read-side the brain decides over: the live friendly roster, fog-of-war enemies, and funds. Built
    /// each tick by the Game layer from the providers, then handed to the pure brain. No Unity types.
    /// </summary>
    public sealed class WorldSnapshot
    {
        public IReadOnlyList<UnitView> Roster { get; }
        public IReadOnlyList<EnemyView> KnownEnemies { get; }
        public float Funds { get; }
        /// <summary>Units the MANUAL layer already committed — the autonomous brain must not poach them.</summary>
        public IReadOnlyCollection<string> CommittedUnitIds { get; }
        /// <summary>Game time (seconds) — stamped onto battle-feed events.</summary>
        public float Time { get; }

        public WorldSnapshot(IReadOnlyList<UnitView> roster, IReadOnlyList<EnemyView> knownEnemies, float funds = 0f,
            IReadOnlyCollection<string> committedUnitIds = null, float time = 0f)
        {
            Roster = roster ?? new List<UnitView>();
            KnownEnemies = knownEnemies ?? new List<EnemyView>();
            Funds = funds;
            CommittedUnitIds = committedUnitIds ?? new List<string>();
            Time = time;
        }
    }
}
