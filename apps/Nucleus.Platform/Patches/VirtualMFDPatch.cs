using HarmonyLib;

namespace CommanderLayer.Patches
{
    /// <summary>
    /// When the map MFD opens, the host attaches each enabled mod's registered bezel button to a blank slot.
    /// VirtualMFD_onMapMaximized runs after the bezel buttons are shown, so the blank slots exist by then.
    /// </summary>
    [HarmonyPatch(typeof(VirtualMFD), "VirtualMFD_onMapMaximized")]
    internal static class VirtualMFDPatch
    {
        [HarmonyPostfix]
        private static void Postfix(VirtualMFD __instance)
        {
            PlatformPlugin.Host?.AttachButtons(__instance);
        }
    }
}
