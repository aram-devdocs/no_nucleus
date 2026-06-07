using BepInEx.Logging;
using Nucleus.Abstractions;
using Nucleus.Core.War;
using Nucleus.Ui;
using UnityEngine;

namespace Nucleus.Host
{
    /// <summary>
    /// Shows the pre-mission setup screen for the Nucleus Dynamic Warfare mode (side select + human/AI per side
    /// + AI auto-fill + START) before the player joins, then applies the choices: writes <see cref="WarSetup"/>
    /// (read by the Warfare mod + Commander) and joins the chosen side. Driven by the MissionManager.Update tick
    /// (the reliable pre-join per-frame hook). Gated to our mission so it never interferes with other missions.
    /// </summary>
    internal sealed class WarSetupController
    {
        private const string ModeMission = "Nucleus Dynamic Warfare";
        private readonly IGameServices _game;
        private readonly ManualLogSource _log;
        private WarSetupScreen _screen;
        private bool _done;          // setup finished (started or not our mode)
        private string _lastMission; // detect mission changes to reset

        public WarSetupController(IGameServices game, ManualLogSource log)
        {
            _game = game;
            _log = log;
        }

        public void Tick()
        {
            if (_game == null) return;
            var mission = _game.CurrentMissionName;

            // Reset when the mission changes (new war -> fresh setup).
            if (mission != _lastMission)
            {
                _lastMission = mission;
                Dismiss();
                _done = false;
                WarSetup.Reset();
            }

            if (_done) return;
            if (string.IsNullOrEmpty(mission) || mission != ModeMission) { _done = true; return; }
            if (_game.HasLocalFaction) { Dismiss(); _done = true; return; } // already joined (e.g. dev harness)
            if (_screen != null) return; // already showing

            var factions = _game.FactionNames();
            if (factions == null || factions.Count < 2) return; // wait for factions to register

            var parent = FindCanvas();
            if (parent == null) return; // no canvas yet — try next frame

            _screen = new WarSetupScreen(parent, Theme.Default, factions, OnStart);
            _log.LogInfo($"[NUCLEUS:SELFTEST] PASS war-setup-shown factions={factions.Count}");
        }

        private void OnStart(string playerFaction, bool aiCommander, bool aiAutoFill)
        {
            WarSetup.PlayerFaction = playerFaction;
            WarSetup.PlayerSideAiCommander = aiCommander;
            WarSetup.AiAutoFill = aiAutoFill;
            WarSetup.Commanders = new System.Collections.Generic.Dictionary<string, CommanderKind>();
            foreach (var f in _game.FactionNames())
                WarSetup.Commanders[f] = f == playerFaction ? CommanderKind.Human : CommanderKind.Ai;
            WarSetup.Configured = true;

            bool joined = _game.JoinFaction(playerFaction);
            _log.LogInfo($"[NUCLEUS:SELFTEST] {(joined ? "PASS" : "FAIL")} war-setup-start faction='{playerFaction}' aiCmd={aiCommander} autoFill={aiAutoFill}");
            Dismiss();
            _done = true;
        }

        private void Dismiss()
        {
            if (_screen != null) { _screen.Destroy(); _screen = null; }
        }

        // The top screen-space-overlay canvas in the scene (the game's HUD canvas), to host the setup window.
        private static Transform FindCanvas()
        {
            Canvas best = null;
            foreach (var c in Object.FindObjectsOfType<Canvas>())
            {
                if (c == null || !c.isActiveAndEnabled) continue;
                if (c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                if (best == null || c.sortingOrder > best.sortingOrder) best = c;
            }
            return best != null ? best.transform : null;
        }
    }
}
