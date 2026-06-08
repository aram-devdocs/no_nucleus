using System;
using HarmonyLib;
using UnityEngine.EventSystems;

namespace Nucleus.Patches
{
    /// <summary>
    /// Drives the mod host from the map's per-frame Update via Harmony (this game does not pump a MonoBehaviour
    /// Update on our own objects, but it does call DynamicMap.Update every frame in a mission). The prefix
    /// suppresses the map's own update (including LMB-drag panning) while the pointer is over any mod UI, so
    /// dragging a panel doesn't pan the map. The postfix ticks every enabled mod via the host registry.
    /// </summary>
    [HarmonyPatch(typeof(DynamicMap), "Update")]
    internal static class DynamicMapUpdateTickPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            // Suppress map pan/zoom while the pointer is over any of our UI (host or mod canvases), OR while a
            // Nucleus panel is being dragged — a fast drag can flick the cursor off the panel, making
            // IsPointerOverGameObject() momentarily false and the map pan bleed through (the panel-drag bug).
            if (Ui.DragHandle.Dragging) return false;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return false;
            }
            return true;
        }

        [HarmonyPostfix]
        private static void Postfix()
        {
            // Tick flows through the mod host (registry -> enabled mods -> Commander's runtime).
            try { PlatformPlugin.Host?.Tick(); }
            catch (Exception e) { PlatformPlugin.Log?.LogError("Update tick threw: " + e); }
            // Dev harness: drive the in-mission phase of the auto-loader (no-op unless armed).
            try { Host.MissionAutoLoader.TickMission(); }
            catch (Exception e) { PlatformPlugin.Log?.LogError("Autoload mission-tick threw: " + e); }
            // NOTE: VisualProbe is driven by its OWN DontDestroyOnLoad MonoBehaviour (ProbeDriver), not here —
            // this patch only fires while the map is maximised, but the probe must keep ticking after it
            // minimises the map to shoot the in-flight HUD.
        }
    }
}
