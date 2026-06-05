using System.Collections.Generic;
using CommanderLayer.Core.Model;
using CommanderLayer.Core.Ports;
using NuclearOption.SavedMission.ObjectiveV2;

namespace CommanderLayer.Game
{
    /// <summary>
    /// IObjectiveService over MissionManager.Runner. Registers a single CommanderObjective for the local
    /// faction (host-side) by adding it to the runner's PUBLIC active-objective lists — the same state the
    /// AI assigner reads. We avoid the runner's internal StartObjective() because, at runtime, the game
    /// assembly keeps it internal and calling it cross-assembly throws MethodAccessException.
    /// </summary>
    public sealed class GameObjectiveService : IObjectiveService
    {
        private CommanderObjective _current;

        public void Place(ObjectiveModel objective)
        {
            var runner = MissionManager.Runner;
            if (runner == null)
            {
                Plugin.Log?.LogWarning("Place objective: no MissionRunner (not in a mission / not host).");
                return;
            }
            if (!GameManager.GetLocalHQ(out var hq) || hq == null)
            {
                Plugin.Log?.LogWarning("Place objective: no local faction HQ.");
                return;
            }

            Clear();

            GameManager.GetLocalFaction(out var faction);
            var obj = new CommanderObjective(
                hq,
                GameConvert.ToGlobal(objective.Position),
                objective.Radius,
                faction != null ? faction.factionName : null);

            // Replicate MissionRunner.AddActiveObjective using public state (no internal call).
            obj.Status = ObjectiveStatus.Running;
            runner.ActiveObjectives.Add(obj);
            if (!runner.activeByFaction.TryGetValue(hq, out var list))
            {
                list = new List<Objective>();
                runner.activeByFaction[hq] = list;
            }
            list.Add(obj);
            obj.OnStart();

            _current = obj;
            Plugin.Log?.LogInfo($"Commander objective placed at {objective.Position} for {(faction != null ? faction.factionName : "?")}.");
        }

        public void Clear()
        {
            var runner = MissionManager.Runner;
            if (_current != null && runner != null)
            {
                runner.StopObjective(_current); // StopObjective is public at runtime
                Plugin.Log?.LogInfo("Commander objective cleared.");
            }
            _current = null;
        }
    }
}
