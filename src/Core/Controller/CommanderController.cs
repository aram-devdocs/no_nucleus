using System;
using System.Collections.Generic;
using System.Linq;
using CommanderLayer.Core.Model;
using CommanderLayer.Core.Ports;

namespace CommanderLayer.Core.Controller
{
    /// <summary>
    /// The presenter / view-model for the commander layer. Holds the single CommanderState, mutates it in
    /// response to player actions, and recomputes the assignment readback from the ports. Pure logic with
    /// no engine or UI dependencies, so it is fully unit-testable against fake ports.
    /// </summary>
    public sealed class CommanderController
    {
        private readonly IPlayerContext _player;
        private readonly IUnitQuery _units;
        private readonly IObjectiveService _objectives;
        private readonly IClock _clock;
        private readonly float _arriveRadius;

        private int _objectiveCounter;
        private ObjectiveModel _objective;
        private bool _armed;

        /// <summary>Raised whenever State changes, so the UI can re-render from the new snapshot.</summary>
        public event Action<CommanderState> StateChanged;

        public CommanderState State { get; private set; } = CommanderState.NoFaction;

        public CommanderController(
            IPlayerContext player,
            IUnitQuery units,
            IObjectiveService objectives,
            IClock clock,
            float arriveRadius = 250f)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _units = units ?? throw new ArgumentNullException(nameof(units));
            _objectives = objectives ?? throw new ArgumentNullException(nameof(objectives));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _arriveRadius = arriveRadius;
        }

        /// <summary>Arm placement: the next map click drops the objective. No-op without a faction.</summary>
        public void ArmPlacement()
        {
            if (!_player.TryGetLocalFaction(out _))
            {
                Rebuild();
                return;
            }
            _armed = true;
            Rebuild();
        }

        public void Disarm()
        {
            _armed = false;
            Rebuild();
        }

        /// <summary>
        /// Drop the objective at a world position. Returns false (and changes nothing) when there is no
        /// local faction. Clears the armed flag on success.
        /// </summary>
        public bool TryPlaceAt(Vec3 world)
        {
            if (!_player.TryGetLocalFaction(out _))
            {
                Rebuild();
                return false;
            }

            _objectiveCounter++;
            _objective = new ObjectiveModel(
                id: "commander-" + _objectiveCounter,
                kind: ObjectiveKind.MoveAttack,
                position: world);
            _objectives.Place(_objective);
            _armed = false;
            Rebuild();
            return true;
        }

        /// <summary>Remove the current objective.</summary>
        public void Clear()
        {
            if (_objective == null)
            {
                Rebuild();
                return;
            }
            _objective = null;
            _objectives.Clear();
            Rebuild();
        }

        /// <summary>Re-read faction + units and recompute the readback. Called each tick by the runtime.</summary>
        public void Refresh() => Rebuild();

        private void Rebuild()
        {
            if (!_player.TryGetLocalFaction(out var faction))
            {
                // Lost the faction (e.g. left the mission): drop local objective state.
                _objective = null;
                _armed = false;
                SetState(CommanderState.NoFaction);
                return;
            }

            var assignments = BuildAssignments(_objective);
            string status = BuildStatus(faction, _objective, _armed, assignments);
            SetState(new CommanderState(true, faction, _objective, _armed, assignments, status));
        }

        private AssignmentSnapshot BuildAssignments(ObjectiveModel objective)
        {
            if (objective == null)
            {
                return AssignmentSnapshot.Empty;
            }

            float arrive = objective.Radius ?? _arriveRadius;
            var ranked = _units.GetFriendlyUnits()
                .Where(u => !u.Disabled)
                .Select(u =>
                {
                    float dist = u.Position.HorizontalDistanceTo(objective.Position);
                    var state = dist <= arrive ? AssignmentState.Arrived : AssignmentState.EnRoute;
                    return new UnitAssignment(u.Name, u.TypeName, u.Position, dist, u.Commandable, state);
                })
                .OrderBy(a => a.DistanceToObjective)
                .ToList();

            return new AssignmentSnapshot(ranked);
        }

        private static string BuildStatus(FactionInfo faction, ObjectiveModel objective, bool armed, AssignmentSnapshot assignments)
        {
            if (armed)
            {
                return "Click the map to place a Move/Attack objective.";
            }
            if (objective != null)
            {
                return $"Objective at {objective.Position} — {assignments.CommandableCount} units tasked, {assignments.Total} nearby.";
            }
            return $"{faction.Name}: no objective. Press Place, then click the map.";
        }

        private void SetState(CommanderState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
