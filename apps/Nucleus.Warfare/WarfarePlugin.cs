using System.IO;
using BepInEx;
using Nucleus.Abstractions;

namespace Nucleus.Warfare
{
    /// <summary>The Warfare mod as its own BepInEx plugin. Registers through <see cref="ModPlatform"/>'s
    /// pending-buffer so load order relative to the host does not matter, and hands the mod a writable save
    /// directory under BepInEx config (never the game install).</summary>
    [BepInPlugin("com.nucleus.warfare", "Nucleus Warfare", "0.1.0")]
    [BepInDependency(ModPlatform.Guid, BepInDependency.DependencyFlags.HardDependency)]
    public class WarfarePlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            var saveDir = Path.Combine(Paths.ConfigPath, "NucleusWarfare");
            ModPlatform.Register(new WarfareMod(Path.Combine(saveDir, "campaign.ncw")));
        }
    }
}
