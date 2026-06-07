using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Nucleus
{
    /// <summary>
    /// The Nucleus platform/host plugin. Loaded first; mods hard-depend on it. Owns the mod host (the single
    /// tick pump + shared game services + bezel-button registry), patches the three contended game methods
    /// (DynamicMap.Update, VirtualMFD.onMapMaximized, MainMenu.Start), wires the shared logging seam, and
    /// persists each mod's enabled state to config.
    /// </summary>
    [BepInPlugin(Guid, "Nucleus Platform", Version)]
    public class PlatformPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.nucleus.platform";
        public const string Version = "0.1.0";

        internal static ManualLogSource Log;
        internal static Host.ModHost Host;
        internal static Host.WarSetupController Setup;

        // Per-mod enabled state, bound lazily to config (Mods.<id>.Enabled) so the loader toggle persists.
        private readonly Dictionary<string, ConfigEntry<bool>> _modEnabled = new Dictionary<string, ConfigEntry<bool>>();
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
            // Shared logging seam so the pure/SDK libs log through BepInEx without referencing this plugin.
            Core.NucleusLog.Info = m => Log.LogInfo(m);
            Core.NucleusLog.Warn = m => Log.LogWarning(m);
            Core.NucleusLog.Error = m => Log.LogError(m);
            Application.runInBackground = true;

            Host = new Host.ModHost(Logger, ModEnabled, SetModEnabled);
            Setup = new Host.WarSetupController(Host.Game, Logger);

            var harmony = new Harmony(Guid);
            ApplyPatch(harmony, typeof(Patches.MainMenuBadgePatch));
            ApplyPatch(harmony, typeof(Patches.MainMenuTickPatch));
            ApplyPatch(harmony, typeof(Patches.MissionManagerTickPatch));
            ApplyPatch(harmony, typeof(Patches.DynamicMapUpdateTickPatch));
            ApplyPatch(harmony, typeof(Patches.VirtualMFDPatch));

            Log.LogInfo("Nucleus Platform loaded.");

            // Dev: optionally dump the game's built-in mission TextAssets so we can fork one (env-gated, no-op off).
            Nucleus.Host.MissionExporter.MaybeExport(Log);
            // Dev test harness: optionally auto-load a mission + emit in-mission markers (trigger-gated, no-op off).
            // Driven by the MainMenu.Update + DynamicMap.Update patches (this game pumps no Update on our objects).
            Nucleus.Host.MissionAutoLoader.Maybe(Log);
            // Dev VISUAL harness: optionally drive the UI + capture screenshots in-mission (trigger-gated, no-op off).
            Nucleus.Host.VisualProbe.Maybe(Log);
        }

        // Game quitting: let each mod tear down (e.g. Warfare persists the campaign so a multi-hour war survives).
        private void OnApplicationQuit() => Host?.Registry.ShutdownAll();

        private static void ApplyPatch(Harmony harmony, Type patchType)
        {
            try { harmony.PatchAll(patchType); Log.LogInfo("Patched: " + patchType.Name); }
            catch (Exception e) { Log.LogError($"Patch {patchType.Name} failed: " + e.Message); }
        }
    }
}
