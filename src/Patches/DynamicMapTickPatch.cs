using System;
using HarmonyLib;
using UnityEngine.EventSystems;

namespace CommanderLayer.Patches
{
    /// <summary>
    /// Drives the commander runtime from the map's per-frame Update via Harmony (this game does not pump a
    /// MonoBehaviour Update on our own objects, but it does call DynamicMap.Update every frame in a mission).
    /// The prefix also suppresses the map's own update (including LMB-drag panning) while the pointer is over
    /// our panel, so dragging the modal doesn't pan the map. The postfix still ticks the runtime.
    /// </summary>
    [HarmonyPatch(typeof(DynamicMap), "Update")]
    internal static class DynamicMapUpdateTickPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            if (Plugin.Runtime != null && Plugin.Runtime.ModalOpen
                && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return false; // skip map Update (no pan/zoom) while interacting with the Commander panel
            }
            return true;
        }

        [HarmonyPostfix]
        private static void Postfix()
        {
            try { Plugin.Runtime?.Tick(); }
            catch (Exception e) { Plugin.Log?.LogError("Update tick threw: " + e); }
        }
    }
}
