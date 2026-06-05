using CommanderLayer.Core.Model;

namespace CommanderLayer.Core.Ports
{
    /// <summary>
    /// Direct per-unit move command — the precision lever (overrides objective-seeking for that unit).
    /// Implemented by the Game layer over UnitCommand.SetDestination.
    /// </summary>
    public interface ICommandService
    {
        void MoveTo(string unitId, Vec3 position);
    }
}
