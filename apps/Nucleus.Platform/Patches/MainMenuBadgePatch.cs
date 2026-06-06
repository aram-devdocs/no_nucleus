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
        private static void Postfix()
        {
            try
            {
                var go = MainMenuBadge.Create($"Nucleus loaded  -  v{PlatformPlugin.Version}");
                Created = go != null;
                PlatformPlugin.Log?.LogInfo(Created ? "Main-menu badge created." : "Main-menu badge: no canvas (IMGUI fallback).");

                // NOTE: the old custom-Canvas "MODS" overlay loader is removed — it was an unresponsive
                // add-on bolted over the native menu. The native main-menu entry (cloned from the game's own
                // menu button + a native page) replaces it in P8 Foundation B. Per-mod enable/disable remains
                // available via the BepInEx ConfigurationManager (Mods.<id>.Enabled) until then.
            }
            catch (Exception e)
            {
                PlatformPlugin.Log?.LogError("Main-menu badge failed: " + e);
            }
        }
    }
}
