using System;
using CommanderLayer.Ui;
using HarmonyLib;

namespace CommanderLayer.Patches
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
                var go = MainMenuBadge.Create($"Commander mod loaded  ▸  v{Plugin.Version}");
                Created = go != null;
                Plugin.Log?.LogInfo(Created ? "Main-menu badge created." : "Main-menu badge: no canvas (IMGUI fallback).");
            }
            catch (Exception e)
            {
                Plugin.Log?.LogError("Main-menu badge failed: " + e);
            }
        }
    }
}
