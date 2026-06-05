using CommanderLayer.Core.Model;
using CommanderLayer.Core.Ports;

namespace CommanderLayer.Game
{
    /// <summary>
    /// ICommandService over UnitCommand.SetDestination — the precision lever that directly steers one
    /// commandable unit (overrides objective-seeking for it). Not on the POC's critical path, but kept
    /// behind the port so the broker style of control is available and testable.
    /// </summary>
    public sealed class GameCommandService : ICommandService
    {
        public void MoveTo(string unitId, Vec3 position)
        {
            foreach (var u in UnitRegistry.allUnits)
            {
                if (u == null || u.GetInstanceID().ToString() != unitId)
                {
                    continue;
                }
                if (u is ICommandable commandable && commandable.UnitCommand != null)
                {
                    commandable.UnitCommand.SetDestination(GameConvert.ToGlobal(position), playerCommand: true);
                }
                return;
            }
        }
    }
}
