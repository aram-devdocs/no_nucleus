using System;
using BepInEx.Logging;
using Nucleus.Game;
using NuclearOption.Networking;
using NuclearOption.SavedMission;
using UnityEngine;

namespace Nucleus.Host
{
    /// <summary>
    /// Dev test harness: programmatically load + start a singleplayer mission and emit in-mission self-test
    /// markers, so in-mission behaviour (objectives, faction binding, scoreboard, the live census) can be
    /// verified WITHOUT a human navigating menus. Gated on a trigger file (&lt;gameroot&gt;/nucleus-autoload.txt,
    /// written by scripts/inmission-smoke.ps1) or the NUCLEUS_AUTOLOAD_MISSION env var; a no-op otherwise.
    ///
    /// This game does NOT pump MonoBehaviour.Update on our plugin objects and runs no coroutines for us, so the
    /// loader is a frame-driven state machine: <see cref="TickMenu"/> is driven by a MainMenu.Update patch (the
    /// only per-frame hook at the menu), issues the load (TryLoad -> SetMission -> StartHost, like
    /// SinglePlayerMenu), then <see cref="TickMission"/> — driven by the in-mission DynamicMap.Update tick —
    /// waits for units and emits the census once.
    /// </summary>
    internal static class MissionAutoLoader
    {
        private enum Phase { Off, WaitNetwork, LoadIssued, Done }
        private static Phase _phase = Phase.Off;
        private static string _name;
        private static ManualLogSource _log;
        private static float _armedAt = -1f;
        private static float _loadAt = -1f;
        private static readonly GameWar _war = new GameWar();

        public static void Maybe(ManualLogSource log)
        {
            string name = null;
            try
            {
                var path = System.IO.Path.Combine(Application.dataPath, "..", "nucleus-autoload.txt");
                if (System.IO.File.Exists(path)) name = System.IO.File.ReadAllText(path).Trim();
            }
            catch { /* fall through to env var */ }
            if (string.IsNullOrEmpty(name)) name = Environment.GetEnvironmentVariable("NUCLEUS_AUTOLOAD_MISSION");
            if (string.IsNullOrEmpty(name)) return; // not in harness mode

            _name = name;
            _log = log;
            _phase = Phase.WaitNetwork;
            log.LogInfo($"[NUCLEUS] mission auto-loader armed for '{name}'");
        }

        /// <summary>Driven every frame at the main menu (MainMenu.Update patch). Waits for networking, then
        /// issues the mission load. After StartHost we leave the menu, so this stops being called.</summary>
        public static void TickMenu()
        {
            if (_phase != Phase.WaitNetwork) return;
            if (_armedAt < 0f) _armedAt = Time.realtimeSinceStartup;

            if (NetworkManagerNuclearOption.i == null)
            {
                if (Time.realtimeSinceStartup - _armedAt > 60f)
                {
                    _log.LogError("[NUCLEUS:SELFTEST] FAIL mission-autoload-no-network");
                    _phase = Phase.Done;
                }
                return;
            }
            if (Time.realtimeSinceStartup - _armedAt < 3f) return; // let the menu settle

            try
            {
                _log.LogInfo("[NUCLEUS] autoload: network up, loading mission...");
                var key = new MissionKey(_name, MissionGroup.User);
                if (!MissionSaveLoad.TryLoad(key, out var mission, out var error) || mission == null)
                {
                    _log.LogError($"[NUCLEUS:SELFTEST] FAIL mission-autoload-load name='{_name}' error='{error}'");
                    _phase = Phase.Done;
                    return;
                }
                _log.LogInfo("[NUCLEUS] autoload: TryLoad ok, SetMission...");
                MissionManager.SetMission(mission, checkIfSame: false);
                _log.LogInfo("[NUCLEUS] autoload: SetMission ok, StartHost...");
                NetworkManagerNuclearOption.i.StartHost(
                    new HostOptions(SocketType.Offline, GameState.SinglePlayer, mission.MapKey));
                _log.LogInfo($"[NUCLEUS:SELFTEST] PASS mission-autoloaded name='{_name}'");
                _phase = Phase.LoadIssued;
                _loadAt = Time.realtimeSinceStartup;
            }
            catch (Exception e)
            {
                _log.LogError("[NUCLEUS:SELFTEST] FAIL mission-autoload-exception " + e);
                _phase = Phase.Done;
            }
        }

        /// <summary>Driven every frame in-mission (DynamicMap.Update tick). Once units have spawned, emit the
        /// census (faction counts, airbases, the game's scripted-objective count) and the in-mission marker.</summary>
        public static void TickMission()
        {
            if (_phase != Phase.LoadIssued) return;

            var census = _war.Census();
            int units = 0;
            foreach (var f in census) units += f.AliveUnits;
            if (units == 0)
            {
                if (Time.realtimeSinceStartup - _loadAt > 120f)
                {
                    _log.LogError("[NUCLEUS:SELFTEST] FAIL inmission-no-units");
                    _phase = Phase.Done;
                }
                return;
            }

            int factions = 0, totalUnits = 0, totalBases = 0;
            foreach (var f in census)
            {
                factions++; totalUnits += f.AliveUnits; totalBases += f.Airbases;
                _log.LogInfo($"[NUCLEUS:METRIC] inmission-faction name='{f.FactionName}' units={f.AliveUnits} airbases={f.Airbases}");
            }
            _log.LogInfo($"[NUCLEUS:METRIC] inmission factions={factions} units={totalUnits} airbases={totalBases} gameObjectives={SafeGameObjectiveCount()}");
            _log.LogInfo("[NUCLEUS:SELFTEST] PASS inmission-units-present");
            _phase = Phase.Done;
        }

        // The game's own scripted objective count (we want this at zero for the dynamic-war mode — only Nucleus
        // objectives should drive). Defensive: any access failure reports -1 rather than throwing.
        private static int SafeGameObjectiveCount()
        {
            try
            {
                var objs = MissionManager.Objectives;
                return objs?.AllObjectives?.Count ?? 0;
            }
            catch { return -1; }
        }
    }
}
