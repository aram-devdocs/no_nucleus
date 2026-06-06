using BepInEx;
using CommanderLayer.Abstractions;

namespace Nucleus.Build
{
    /// <summary>
    /// The Build mod as its own BepInEx plugin — the first mod that ships separately from the host. It does
    /// not hard-depend on the platform GUID (the host currently runs inside the Commander plugin); instead it
    /// registers through <see cref="ModPlatform"/>, whose pending-buffer holds the registration until the host
    /// installs its handler, so load order does not matter. (Soft dependency is forward-compat for when the
    /// platform is extracted as its own com.nucleus.platform plugin.)
    /// </summary>
    [BepInPlugin("com.nucleus.build", "Nucleus Build", "0.1.0")]
    [BepInDependency(ModPlatform.Guid, BepInDependency.DependencyFlags.HardDependency)]
    public class BuildPlugin : BaseUnityPlugin
    {
        private void Awake() => ModPlatform.Register(new BuildMod());
    }
}
