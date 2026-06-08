using System;
using BepInEx;
using BepInEx.Logging;
using Nucleus.Abstractions;
using Nucleus.Commander;
using Nucleus.Composition;
using HarmonyLib;
using UnityEngine;

namespace Nucleus
{
    /// <summary>
    /// The Commander mod plugin: binds the Commander config (F1 menu), builds the runtime, and registers the
    /// Commander mod with the platform. Hard-depends on the platform so the host loads first.
    /// </summary>
    [BepInPlugin(Guid, "Nucleus Commander", Version)]
    [BepInDependency(ModPlatform.Guid, BepInDependency.DependencyFlags.HardDependency)]
    public class CommanderPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.nucleus.commander";
        public const string Version = "0.1.0";

        internal static ManualLogSource Log;
        internal static CommanderRuntime Runtime;

        // Config (tunable from the F1 menu), read by the runtime/service.
        internal static KeyCode ArmKey = KeyCode.G;
        internal static bool EnableAircraftTasking;
        internal static bool EnableAutoCommander;
        internal static bool CommanderDebug;
        internal static bool ShowFlightHud = true;
        internal static KeyCode HudToggleKey = KeyCode.H;

        private void Awake()
        {
            Log = Logger;

            var keyCfg = Config.Bind("Commander", "ArmPlacementKey", KeyCode.G,
                "Optional key (while the map is open) to arm objective placement; then click the map.");
            var airCfg = Config.Bind("Commander", "EnableAircraftTasking", false,
                "Let the Commander steer IDLE friendly aircraft toward its air objectives. Off by default so the native AI flies them.");
            var autoCfg = Config.Bind("Commander", "EnableAutoCommander", false,
                "Hand idle forces to the autonomous Commander. Off by default = the native game AI runs the war; turn on to let the Commander coordinate.");
            var dbgCfg = Config.Bind("Commander", "CommanderDebug", false,
                "Instrumentation: log debug lines (unit ids, kill tracking, terrain) for one playtest.");
            var hudCfg = Config.Bind("Commander", "ShowFlightHud", true,
                "Show a compact objective HUD in the bottom-right while flying (map closed).");
            var hudKeyCfg = Config.Bind("Commander", "FlightHudToggleKey", KeyCode.H,
                "Key to show/hide the in-flight objective HUD.");

            ArmKey = keyCfg.Value;
            EnableAircraftTasking = airCfg.Value;
            EnableAutoCommander = autoCfg.Value;
            CommanderDebug = dbgCfg.Value;
            ShowFlightHud = hudCfg.Value;
            HudToggleKey = hudKeyCfg.Value;
            hudCfg.SettingChanged += (_, __) => ShowFlightHud = hudCfg.Value;
            hudKeyCfg.SettingChanged += (_, __) => HudToggleKey = hudKeyCfg.Value;
            Game.AircraftIntent.Enabled = airCfg.Value;
            autoCfg.SettingChanged += (_, __) => EnableAutoCommander = autoCfg.Value;
            dbgCfg.SettingChanged += (_, __) => CommanderDebug = dbgCfg.Value;
            keyCfg.SettingChanged += (_, __) => ArmKey = keyCfg.Value;
            airCfg.SettingChanged += (_, __) => { EnableAircraftTasking = airCfg.Value; Game.AircraftIntent.Enabled = airCfg.Value; };

            Runtime = new CommanderRuntime();
            ModPlatform.Register(new CommanderMod(Runtime));

            var harmony = new Harmony(Guid);
            try { harmony.PatchAll(typeof(Patches.AircraftTaskingPatch)); Log.LogInfo("Patched: AircraftTaskingPatch"); }
            catch (Exception e) { Log.LogError("Patch AircraftTaskingPatch failed: " + e.Message); }
            try { harmony.PatchAll(typeof(Patches.CommanderHudTickPatch)); Log.LogInfo("Patched: CommanderHudTickPatch"); }
            catch (Exception e) { Log.LogError("Patch CommanderHudTickPatch failed: " + e.Message); }

            Log.LogInfo("Nucleus Commander loaded.");
        }
    }
}
