using CommanderLayer.Core.Model;
using CommanderLayer.Core.Ports;
using NuclearOption.Networking;

namespace CommanderLayer.Game
{
    /// <summary>IPlayerContext over GameManager (global static accessors).</summary>
    public sealed class GamePlayerContext : IPlayerContext
    {
        public bool IsHost
        {
            get
            {
                try
                {
                    return NetworkManagerNuclearOption.i.Server.Active;
                }
                catch
                {
                    // Singleton not ready (menus) — treat as host so single-player still works.
                    return true;
                }
            }
        }

        public bool TryGetLocalFaction(out FactionInfo faction)
        {
            faction = null;
            if (!GameManager.GetLocalFaction(out var f) || f == null)
            {
                return false;
            }
            string name = !string.IsNullOrEmpty(f.factionExtendedName) ? f.factionExtendedName : f.factionName;
            faction = new FactionInfo(name ?? "Faction", GameConvert.ToRgba(f.color));
            return true;
        }
    }
}
