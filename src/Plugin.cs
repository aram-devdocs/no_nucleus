using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CommanderLayer.Composition;
using HarmonyLib;
using UnityEngine;

namespace CommanderLayer
{
    /// <summary>
    /// BepInEx entry point and composition root. Binds config (visible in the F1 ConfigurationManager),
    /// applies the menu-badge Harmony patch, and spawns the persistent runtime that owns everything else.
    /// </summary>
    [BepInPlugin(Guid, "Commander Layer", Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.commanderlayer.mod";
        public const string Version = "0.1.0";

        internal static ManualLogSource Log;

        // Config (tunable live from the F1 menu). Read once by the runtime/controller at build time.
        internal static float ArriveRadius = 250f;
        internal static KeyCode ArmKey = KeyCode.G;
        internal static bool EnableAircraftTasking;
        internal static bool EnableAutoCommander;
        internal static bool CommanderDebug;

        internal static CommanderRuntime Runtime;

        private void Awake()
        {
            Log = Logger;
            // Keep updating (and flushing logs) while the window is unfocused, so diagnostics survive alt-tab.
            Application.runInBackground = true;

            var arriveCfg = Config.Bind("Commander", "ArriveRadius", 250f,
                "Distance (m) within which a unit counts as 'arrived' at the objective.");
            var keyCfg = Config.Bind("Commander", "ArmPlacementKey", KeyCode.G,
                "Optional key (while the map is open) to arm objective placement; then click the map.");
            var airCfg = Config.Bind("Commander", "EnableAircraftTasking", false,
                "EXPERIMENTAL: steer idle friendly aircraft toward Air-domain commander orders (needs in-game tuning).");
            var autoCfg = Config.Bind("Commander", "EnableAutoCommander", false,
                "EXPERIMENTAL: the autonomous commander brain auto-generates objectives and tasks squads. Needs playtest validation; may overlap with manual orders. Off = the game runs as normal.");
            var dbgCfg = Config.Bind("Commander", "CommanderDebug", false,
                "S0 instrumentation: log [S0:*] lines (unit ids, kill tracking, terrain water/land) for one playtest.");
            ArriveRadius = arriveCfg.Value;
            ArmKey = keyCfg.Value;
            EnableAircraftTasking = airCfg.Value;
            EnableAutoCommander = autoCfg.Value;
            CommanderDebug = dbgCfg.Value;
            Game.AircraftIntent.Enabled = airCfg.Value;
            autoCfg.SettingChanged += (_, __) => EnableAutoCommander = autoCfg.Value;
            dbgCfg.SettingChanged += (_, __) => CommanderDebug = dbgCfg.Value;
            arriveCfg.SettingChanged += (_, __) => ArriveRadius = arriveCfg.Value;
            keyCfg.SettingChanged += (_, __) => ArmKey = keyCfg.Value;
            airCfg.SettingChanged += (_, __) => { EnableAircraftTasking = airCfg.Value; Game.AircraftIntent.Enabled = airCfg.Value; };

            Runtime = new CommanderRuntime();

            var harmony = new Harmony(Guid);
            ApplyPatch(harmony, typeof(Patches.MainMenuBadgePatch));
            ApplyPatch(harmony, typeof(Patches.DynamicMapUpdateTickPatch));
            ApplyPatch(harmony, typeof(Patches.VirtualMFDPatch));
            ApplyPatch(harmony, typeof(Patches.AircraftTaskingPatch));

            Log.LogInfo("Commander Layer loaded.");
        }

        private static void ApplyPatch(Harmony harmony, Type patchType)
        {
            try
            {
                harmony.PatchAll(patchType);
                Log.LogInfo("Patched: " + patchType.Name);
            }
            catch (Exception e)
            {
                Log.LogError($"Patch {patchType.Name} failed: " + e.Message);
            }
        }

        private int _frame;
        private void Update()
        {
            // Diagnostic only: tells us whether the plugin's own Update is pumped (the runtime is driven
            // by the DynamicMap.Update Harmony postfix, which is confirmed to run).
            if (_frame++ == 0)
            {
                Log.LogInfo("Plugin.Update is alive.");
            }
        }

        private void OnGUI()
        {
            Runtime?.DrawMenuFallback();
        }
    }
}
