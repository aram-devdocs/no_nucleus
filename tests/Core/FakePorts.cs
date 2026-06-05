using System.Collections.Generic;
using CommanderLayer.Core.Model;
using CommanderLayer.Core.Ports;

namespace CommanderLayer.Tests
{
    internal sealed class FakePlayerContext : IPlayerContext
    {
        public bool IsHost { get; set; } = true;
        public FactionInfo Faction { get; set; }
        public bool TryGetLocalFaction(out FactionInfo faction)
        {
            faction = Faction;
            return Faction != null;
        }
    }

    internal sealed class FakeUnitQuery : IUnitQuery
    {
        public List<UnitInfo> Units { get; } = new List<UnitInfo>();
        public IReadOnlyList<UnitInfo> GetFriendlyUnits() => Units;
    }

    internal sealed class FakeObjectiveService : IObjectiveService
    {
        public int PlaceCount { get; private set; }
        public int ClearCount { get; private set; }
        public ObjectiveModel LastPlaced { get; private set; }

        public void Place(ObjectiveModel objective)
        {
            PlaceCount++;
            LastPlaced = objective;
        }

        public void Clear() => ClearCount++;
    }

    internal sealed class FakeCommandService : ICommandService
    {
        public readonly List<(string unitId, Vec3 pos)> Commands = new List<(string, Vec3)>();
        public void MoveTo(string unitId, Vec3 position) => Commands.Add((unitId, position));
    }

    internal sealed class FakeClock : IClock
    {
        public float Now { get; set; }
    }
}
