using System;
using HarmonyLib;

namespace Nucleus.Patches
{
    /// <summary>
    /// Drives the in-flight objective HUD from MissionManager.Update — which ticks every frame in-mission
    /// REGARDLESS of map state. The rest of the Commander runtime ticks off DynamicMap.Update, which stops
    /// firing while the map is closed (exactly when the flight HUD must be visible), so the HUD needs its own
    /// always-running hook. No-op until the runtime exists.
    /// </summary>
    [HarmonyPatch(typeof(MissionManager), "Update")]
    internal static class CommanderHudTickPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            try { CommanderPlugin.Runtime?.TickHud(); }
            catch (Exception e) { CommanderPlugin.Log?.LogError("HUD tick threw: " + e); }
        }
    }
}
