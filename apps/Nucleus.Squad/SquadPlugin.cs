using BepInEx;
using CommanderLayer.Abstractions;

namespace Nucleus.Squad
{
    /// <summary>
    /// The Squad mod as its own BepInEx plugin (the bridge from build output to commanded forces). Registers
    /// through <see cref="ModPlatform"/>'s pending-buffer so load order relative to the host does not matter.
    /// </summary>
    [BepInPlugin("com.nucleus.squad", "Nucleus Squad", "0.1.0")]
    [BepInDependency(ModPlatform.Guid, BepInDependency.DependencyFlags.HardDependency)]
    public class SquadPlugin : BaseUnityPlugin
    {
        private void Awake() => ModPlatform.Register(new SquadMod());
    }
}
