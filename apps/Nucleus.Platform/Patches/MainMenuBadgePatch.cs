using System;
using Nucleus.Ui;
using HarmonyLib;

namespace Nucleus.Patches
{
    /// <summary>
    /// Postfix on MainMenu.Start: spawns the "Commander mod loaded" badge once the menu is built. Best
    /// effort — failures are logged and leave the IMGUI fallback (in CommanderRuntime) to confirm load.
    /// </summary>
    [HarmonyPatch(typeof(MainMenu), "Start")]
    internal static class MainMenuBadgePatch
    {
        /// <summary>True once the uGUI badge has been created, so the IMGUI fallback can stand down.</summary>
        public static bool Created;

        [HarmonyPostfix]
        private static void Postfix(MainMenu __instance)
        {
            try
            {
                var go = MainMenuBadge.Create($"Nucleus loaded  -  v{PlatformPlugin.Version}");
                Created = go != null;
                PlatformPlugin.Log?.LogInfo(Created ? "Main-menu badge created." : "Main-menu badge: no canvas (IMGUI fallback).");

                // Add a native "NUCLEUS" button into the game's own main menu (cloned from the missions button)
                // that opens a native panel listing the mods with ON/OFF toggles. No custom overlay canvas.
                Host.NativeMenu.Build(__instance, PlatformPlugin.Host?.Registry);
            }
            catch (Exception e)
            {
                PlatformPlugin.Log?.LogError("Main-menu badge failed: " + e);
            }
        }
    }

    /// <summary>Postfix on MainMenu.Update — the only per-frame hook at the menu (this game pumps no Update on
    /// our plugin objects). Drives the dev mission auto-loader's menu phase (no-op unless the harness armed it).</summary>
    [HarmonyPatch(typeof(MainMenu), "Update")]
    internal static class MainMenuTickPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            try { Host.MissionAutoLoader.TickMenu(); }
            catch (Exception e) { PlatformPlugin.Log?.LogError("MainMenu tick threw: " + e); }
        }
    }

    /// <summary>Postfix on MissionManager.Update — runs every frame once a mission is active (before/independent
    /// of the player spawning, unlike DynamicMap.Update). Drives the auto-loader's in-mission census phase.</summary>
    [HarmonyPatch(typeof(MissionManager), "Update")]
    internal static class MissionManagerTickPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            try { Host.MissionAutoLoader.TickMission(); }
            catch (Exception e) { PlatformPlugin.Log?.LogError("MissionManager tick threw: " + e); }
            // Drive the pre-mission setup screen (side select + human/AI per side + START). No-op off our mode.
            try { PlatformPlugin.Setup?.Tick(); }
            catch (Exception e) { PlatformPlugin.Log?.LogError("War setup tick threw: " + e); }
        }
    }
}
