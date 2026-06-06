using HarmonyLib;

namespace CommanderLayer.Patches
{
    /// <summary>
    /// When the map MFD opens, attach our CMD button to a blank bezel slot. VirtualMFD_onMapMaximized runs
    /// after the bezel buttons are shown, so the blank slots exist and are active by the time we hook in.
    /// </summary>
    [HarmonyPatch(typeof(VirtualMFD), "VirtualMFD_onMapMaximized")]
    internal static class VirtualMFDPatch
    {
        [HarmonyPostfix]
        private static void Postfix(VirtualMFD __instance)
        {
            // Commander claims its CMD slot first (proven path); then the host attaches the other mods'
            // registered bezel buttons (BLD/SQD/...) to the remaining blank slots.
            Plugin.Runtime?.AttachCmdButton(__instance);
            Plugin.Host?.AttachButtons(__instance);
        }
    }
}
