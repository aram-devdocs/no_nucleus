using CommanderLayer.Core.Model;

namespace CommanderLayer.Core.Ports
{
    /// <summary>
    /// Registers/removes a commander objective with the game's faction objective list, which its AI
    /// assigner consumes. Implemented by the Game layer over MissionManager.Runner.
    /// </summary>
    public interface IObjectiveService
    {
        /// <summary>Replace any current commander objective with this one (host-side).</summary>
        void Place(ObjectiveModel objective);

        /// <summary>Remove the current commander objective, if any.</summary>
        void Clear();
    }
}
