using System.Collections.Generic;
using NuclearOption.SavedMission.ObjectiveV2;
using NuclearOption.SavedMission.ObjectiveV2.Objectives;

namespace CommanderLayer.Game
{
    /// <summary>
    /// A faction objective with a world position. The game's AI assigner
    /// (MissionPosition.TryGetClosestObjectivePosition) pulls idle friendly units toward the nearest
    /// IObjectiveWithPosition for their faction, so registering this on the host tasks the AI.
    ///
    /// It subclasses the concrete NoObjective (not the abstract Objective) so we don't re-declare the
    /// base's protected abstract members — at runtime the game assembly keeps them protected, and a
    /// public override (forced by the publicized compile reference) would fail to load. We only override
    /// the public UpdateAndCheck to never self-complete, and add the position interface.
    /// </summary>
    public sealed class CommanderObjective : NoObjective, IObjectiveWithPosition
    {
        private readonly List<ObjectivePosition> _positions;

        public IReadOnlyList<ObjectivePosition> Positions => _positions;

        public CommanderObjective(FactionHQ hq, GlobalPosition position, float? range, string factionName)
        {
            _positions = new List<ObjectivePosition> { new ObjectivePosition(position, range) };
            FactionHQ = hq;
            SavedObjective = new SavedObjective("Commander Objective", ObjectiveType.None)
            {
                Faction = factionName ?? string.Empty,
                Hidden = false
            };
        }

        // NoObjective completes immediately; we must persist until explicitly removed.
        public override bool UpdateAndCheck() => false;
    }
}
