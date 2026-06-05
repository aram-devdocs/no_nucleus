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
            Plugin.Runtime?.AttachCmdButton(__instance);
        }
    }
}
