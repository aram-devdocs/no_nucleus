using CommanderLayer.Core.Model;

namespace CommanderLayer.Core.Ports
{
    /// <summary>Access to the local player's faction. Implemented by the Game layer over GameManager.</summary>
    public interface IPlayerContext
    {
        /// <summary>True when this peer is the authoritative host (objective/command mutations are valid).</summary>
        bool IsHost { get; }

        /// <summary>Returns the local player's faction, or false if not yet joined to one.</summary>
        bool TryGetLocalFaction(out FactionInfo faction);
    }
}
