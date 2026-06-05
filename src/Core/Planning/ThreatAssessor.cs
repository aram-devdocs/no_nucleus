using System.Collections.Generic;
using CommanderLayer.Core.Model;

namespace CommanderLayer.Core.Planning
{
    /// <summary>Builds a ThreatPicture from the (fog-of-war) enemies known near an order point.</summary>
    public static class ThreatAssessor
    {
        public static ThreatPicture Assess(IReadOnlyList<EnemyView> enemiesNearPoint)
        {
            return new ThreatPicture(enemiesNearPoint ?? new List<EnemyView>());
        }
    }
}
