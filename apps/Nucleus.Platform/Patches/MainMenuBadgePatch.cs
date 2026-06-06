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
                var go = MainMenuBadge.Create($"Nucleus loaded  -  v{PlatformPlugin.Version}");
                Created = go != null;
                PlatformPlugin.Log?.LogInfo(Created ? "Main-menu badge created." : "Main-menu badge: no canvas (IMGUI fallback).");

                // The mod loader: a MODS button + a panel listing registered mods with per-mod toggles.
                Host.MainMenuLoader.Build(PlatformPlugin.Host?.Registry);
            }
            catch (Exception e)
            {
                PlatformPlugin.Log?.LogError("Main-menu badge failed: " + e);
            }
        }
    }
}
