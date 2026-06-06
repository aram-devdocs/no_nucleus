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
        // Default OFF so the mod WORKS WITH the native game AI, not against it: do nothing and the game's own
        // AI runs the war as normal. Turn these on to hand idle forces to the Commander layer (it then directs
        // them via the game's own command API — a coordination overlay, not a replacement).
        internal static bool EnableAircraftTasking;
        internal static bool EnableAutoCommander;
        internal static bool CommanderDebug;

        internal static CommanderRuntime Runtime;
        internal static Host.ModHost Host;

        // Per-mod enabled state, bound lazily to the F1 config (Mods.<id>.Enabled) so the loader toggle persists.
        private readonly System.Collections.Generic.Dictionary<string, ConfigEntry<bool>> _modEnabled
            = new System.Collections.Generic.Dictionary<string, ConfigEntry<bool>>();
        private ConfigEntry<bool> ModEntry(string id)
        {
            if (!_modEnabled.TryGetValue(id, out var e))
            {
                e = Config.Bind("Mods", id + ".Enabled", true, $"Enable the '{id}' mod.");
                _modEnabled[id] = e;
            }
            return e;
        }
        private bool ModEnabled(string id) => ModEntry(id).Value;
        private void SetModEnabled(string id, bool on) => ModEntry(id).Value = on;

        private void Awake()
        {
            Log = Logger;
            // Wire the shared logging seam so the pure/SDK libs (Nucleus.GameSdk etc.) log through BepInEx
            // without referencing this plugin. Defaults are no-ops (e.g. under unit tests).
            Core.NucleusLog.Info = m => Log.LogInfo(m);
            Core.NucleusLog.Warn = m => Log.LogWarning(m);
            Core.NucleusLog.Error = m => Log.LogError(m);
            // Keep updating (and flushing logs) while the window is unfocused, so diagnostics survive alt-tab.
            Application.runInBackground = true;

            var arriveCfg = Config.Bind("Commander", "ArriveRadius", 250f,
                "Distance (m) within which a unit counts as 'arrived' at the objective.");
            var keyCfg = Config.Bind("Commander", "ArmPlacementKey", KeyCode.G,
                "Optional key (while the map is open) to arm objective placement; then click the map.");
            var airCfg = Config.Bind("Commander", "EnableAircraftTasking", false,
                "Let the Commander steer IDLE friendly aircraft toward its air objectives (they revert to the game's combat AI on contact). Off by default so the native AI flies them.");
            var autoCfg = Config.Bind("Commander", "EnableAutoCommander", false,
                "Hand idle forces to the autonomous Commander: it generates objectives and directs auto-formed squads via the game's own command API. Off by default = the native game AI runs the war; turn on to let the Commander coordinate.");
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

            // Stand up the in-process mod host and register Commander as the first hosted mod. The per-frame
            // tick now flows through the host registry (Host.Tick -> registry -> CommanderMod -> runtime),
            // introducing the platform pattern with behavior preserved (single plugin, Phase 3).
            // Per-mod enabled state persists in the F1 config (Mods.<id>.Enabled), so the loader toggle is
            // remembered across launches.
            Host = new Host.ModHost(Logger, ModEnabled, SetModEnabled);
            Abstractions.ModPlatform.Register(new Host.CommanderMod(Runtime));

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
