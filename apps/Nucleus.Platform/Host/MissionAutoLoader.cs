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
        private enum Phase { Off, WaitNetwork, LoadIssued, Joining, Probing, Done }
        private static Phase _phase = Phase.Off;
        private static string _name;
        private static string _faction;     // optional: join this side and probe player-side behaviour
        private static ManualLogSource _log;
        private static float _armedAt = -1f;
        private static float _loadAt = -1f;
        private static float _joinAt = -1f;
        private static readonly GameWar _war = new GameWar();
        private static readonly GameRoster _roster = new GameRoster();

        public static void Maybe(ManualLogSource log)
        {
            string raw = null;
            try
            {
                var path = System.IO.Path.Combine(Application.dataPath, "..", "nucleus-autoload.txt");
                if (System.IO.File.Exists(path)) raw = System.IO.File.ReadAllText(path).Trim();
            }
            catch { /* fall through to env var */ }
            if (string.IsNullOrEmpty(raw)) raw = Environment.GetEnvironmentVariable("NUCLEUS_AUTOLOAD_MISSION");
            if (string.IsNullOrEmpty(raw)) return; // not in harness mode

            // Format: "Mission Name" or "Mission Name|Faction" (join that side and probe player-side behaviour).
            var parts = raw.Split('|');
            _name = parts[0].Trim();
            _faction = parts.Length > 1 ? parts[1].Trim() : null;
            _log = log;
            _phase = Phase.WaitNetwork;
            log.LogInfo($"[NUCLEUS] mission auto-loader armed for '{_name}'" + (_faction != null ? $" join='{_faction}'" : ""));
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
            if (_phase == Phase.LoadIssued) { TickCensus(); return; }
            if (_phase == Phase.Joining) { TickJoin(); return; }
            if (_phase == Phase.Probing) { TickProbe(); return; }
        }

        private static void TickCensus()
        {
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
            _phase = _faction != null ? Phase.Joining : Phase.Done;
        }

        // Join the requested side (replicates JoinMenu.JoinFaction) so the Commander brain runs for a local
        // faction — lets the harness verify player-side behaviour (objectives, phantom-objective regression).
        private static void TickJoin()
        {
            try
            {
                if (!GameManager.GetLocalPlayer<Player>(out var player) || player == null) return; // wait for local player
                var hq = FactionRegistry.HqFromName(_faction);
                if (hq == null) { _log.LogError($"[NUCLEUS:SELFTEST] FAIL join-no-hq faction='{_faction}'"); _phase = Phase.Done; return; }

                player.SetFaction(hq);
                var map = SceneSingleton<DynamicMap>.i;
                if (map != null) { map.SetFaction(hq); map.Maximize(); }
                _log.LogInfo($"[NUCLEUS:SELFTEST] PASS joined-faction name='{_faction}'");
                _phase = Phase.Probing;
                _joinAt = Time.realtimeSinceStartup;
            }
            catch (Exception e)
            {
                _log.LogError("[NUCLEUS:SELFTEST] FAIL join-exception " + e);
                _phase = Phase.Done;
            }
        }

        // After joining, let the Commander brain run, then probe OUR objectives: count them, and count how many
        // sit on top of a FRIENDLY unit (within 2 km) — the #17 phantom-objective regression. Should be 0.
        private static void TickProbe()
        {
            if (Time.realtimeSinceStartup - _joinAt < 8f) return; // give the brain time to generate objectives
            try
            {
                var roster = _roster.BuildRoster();
                var hq = PlatformPlugin.Host?.Campaign?.Hq();
                int ours = hq?.Operations?.Count ?? 0;
                int enemies = new GameIntel().KnownEnemiesNear(new Nucleus.Core.Model.Vec3(0, 0, 0), 5_000_000f).Count;

                int phantom = 0;
                if (hq?.Operations != null)
                {
                    foreach (var op in hq.Operations)
                        foreach (var u in roster)
                            if (op.Position.HorizontalDistanceTo(u.Position) < 2000f) { phantom++; break; }
                }
                int convoys = -1;
                try { if (GameManager.GetLocalHQ(out var lhq) && lhq?.faction != null) convoys = lhq.faction.GetConvoyGroups().Count; } catch { }
                _log.LogInfo($"[NUCLEUS:METRIC] postjoin roster={roster.Count} enemies={enemies} ourObjectives={ours} phantomObjectives={phantom} convoyGroups={convoys}");
                _log.LogInfo(phantom == 0
                    ? "[NUCLEUS:SELFTEST] PASS no-phantom-objectives"
                    : "[NUCLEUS:SELFTEST] FAIL phantom-objectives-on-friendlies");
            }
            catch (Exception e)
            {
                _log.LogError("[NUCLEUS:SELFTEST] FAIL probe-exception " + e);
            }
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
