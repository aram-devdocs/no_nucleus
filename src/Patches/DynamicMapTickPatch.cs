using System;
using HarmonyLib;

namespace CommanderLayer.Patches
{
    /// <summary>
    /// Drives the commander runtime from the map's per-frame Update via Harmony (this game does not pump a
    /// MonoBehaviour Update on our own objects, but it does call DynamicMap.Update every frame in a mission).
    /// </summary>
    [HarmonyPatch(typeof(DynamicMap), "Update")]
    internal static class DynamicMapUpdateTickPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            try { Plugin.Runtime?.Tick(); }
            catch (Exception e) { Plugin.Log?.LogError("Update tick threw: " + e); }
        }
    }
}
