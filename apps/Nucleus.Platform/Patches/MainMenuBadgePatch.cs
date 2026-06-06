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
}
