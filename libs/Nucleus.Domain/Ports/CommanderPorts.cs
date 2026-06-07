using System.Collections.Generic;
using Nucleus.Core.Command;
using Nucleus.Core.Model;

namespace Nucleus.Core.Ports
{
    // The read (provider) + write (executor) seams the CommanderBrain depends on. The brain is a pure
    // function of these, so the SAME brain drives the local player, an AI commander, and (future) networked
    // commanders — only the Game-layer implementations swap. Implemented by the Game-layer adapters
    // (GameRoster / GameIntel / GameUnitCommands).

    /// <summary>Live friendly roster (classified, Unity-free).</summary>
    public interface IForceProvider
    {
        IReadOnlyList<UnitView> Roster();
    }

    /// <summary>Fog-of-war intel: enemies the faction has detected near a point (last-known positions).</summary>
    public interface IIntelProvider
    {
        IReadOnlyList<EnemyView> KnownEnemiesNear(Vec3 center, float radius);
    }

    /// <summary>Executes per-unit tasking + sets aircraft intent zones. The ONLY write path (no faction objectives).</summary>
    public interface ITaskingExecutor
    {
        void Execute(UnitTask task);
        void SetAircraftZones(IEnumerable<Vec3> zones);
    }

    /// <summary>Economy: available funds and (later) production queueing.</summary>
    public interface IProductionProvider
    {
        float Funds();
    }

    /// <summary>Supplies the doctrine in force (per level later).</summary>
    public interface IDoctrineProvider
    {
        Doctrine Current();
    }
}
