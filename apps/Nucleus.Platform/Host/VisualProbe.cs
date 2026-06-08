using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;

namespace Nucleus.Host
{
    /// <summary>Dev visual harness: once in a mission, drives the mod UI (map min/maximise, open each bezel
    /// panel) and captures a PNG per beat, so UI changes can be verified headlessly. Writes to
    /// &lt;gameroot&gt;/nucleus-shots/ and emits one <c>[NUCLEUS:SHOT]</c> marker per capture.
    /// Gated on a trigger file (&lt;gameroot&gt;/nucleus-autoshot.txt) or the NUCLEUS_AUTOSHOT env var; a no-op
    /// otherwise. Frame-driven state machine ticked from the in-mission patch, after a warm-up.</summary>
    internal static class VisualProbe
    {
        private const float WarmupSec = 12f;     // let mission load + faction join + brain spin up first
        private const float FileWaitSec = 6f;    // max wait for a capture's PNG to hit disk

        private enum Step { Acting, Settling, Capturing, AwaitingFile, Advance }

        private sealed class Shot
        {
            public string Name;
            public Action Action;     // performed once at the start of the shot (may be null)
            public float SettleSec;   // wait after the action before capturing (UI needs frames to render)
        }

        private static bool _armed;
        private static bool _done;
        private static ManualLogSource _log;
        private static string _dir;
        private static float _firstTickAt = -1f;
        private static float _stateAt = -1f;
        private static int _i;          // current shot index
        private static Step _step = Step.Acting;
        private static int _captured;
        private static string _pendingFile;
        private static string _tracePath;
        private static bool _beganLogged;
        private static readonly List<Shot> _shots = new List<Shot>();

        // Self-flushing trace to a plain file — survives a force-kill of the game (BepInEx's log buffers and
        // loses its tail when we Stop-Process), so this is the ground truth for debugging the probe.
        private static void Trace(string msg)
        {
            try { File.AppendAllText(_tracePath, $"{Time.realtimeSinceStartup:0.0}  {msg}\n"); } catch { }
        }

        public static void Maybe(ManualLogSource log)
        {
            string raw = null;
            try
            {
                var path = Path.Combine(Application.dataPath, "..", "nucleus-autoshot.txt");
                if (File.Exists(path)) raw = File.ReadAllText(path).Trim();
            }
            catch { /* fall through to env var */ }
            if (string.IsNullOrEmpty(raw)) raw = Environment.GetEnvironmentVariable("NUCLEUS_AUTOSHOT");
            if (string.IsNullOrEmpty(raw)) return; // not in visual-harness mode

            _log = log;
            try
            {
                _dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "nucleus-shots"));
                Directory.CreateDirectory(_dir);
                _tracePath = Path.Combine(_dir, "probe-trace.log");
                try { if (File.Exists(_tracePath)) File.Delete(_tracePath); } catch { }
            }
            catch (Exception e) { log.LogError("[NUCLEUS:SELFTEST] FAIL shots-dir " + e.Message); return; }

            BuildShots();
            _armed = true;
            log.LogInfo($"[NUCLEUS] visual probe armed — {_shots.Count} shots -> '{_dir}'");
            Trace($"armed shots={_shots.Count} dir={_dir}");
            // Driven by MissionManagerTickPatch (MissionManager.Update) — it runs every frame in-mission
            // REGARDLESS of map state, so the tick survives minimising the map to shoot the in-flight HUD
            // (the DynamicMap.Update patch only fires while the map is maximised; the game pumps no Update on
            // our own MonoBehaviours, so a custom driver GameObject does not tick either).
        }

        private static void BuildShots()
        {
            _shots.Clear();
            // 1) In-flight: map minimised so we see the cockpit + any in-flight HUD.
            _shots.Add(new Shot { Name = "01-inflight", SettleSec = 2.5f, Action = () => Map()?.Minimize() });
            // 2) Command view: map maximised (overlay + whatever panel is open).
            _shots.Add(new Shot { Name = "02-map-open", SettleSec = 3.0f, Action = () => Map()?.Maximize() });
            // 3-6) Each mod panel, opened by invoking its native bezel button's click.
            _shots.Add(new Shot { Name = "03-cmd", SettleSec = 2.0f, Action = () => OpenPanel("commander") });
            // Select the first objective so the map shows the selection detail (header + squad lines/labels).
            _shots.Add(new Shot { Name = "03cmd-selected", SettleSec = 2.0f, Action = ClickFirstSelect });
            _shots.Add(new Shot { Name = "04-bld", SettleSec = 2.0f, Action = () => OpenPanel("build") });
            _shots.Add(new Shot { Name = "05-sqd", SettleSec = 2.0f, Action = () => OpenPanel("squad") });
            _shots.Add(new Shot { Name = "06-war", SettleSec = 2.0f, Action = () => OpenPanel("warfare") });
        }

        /// <summary>Driven every in-mission frame by the DynamicMap.Update patch (no-op unless armed).</summary>
        public static void TickMission()
        {
            if (!_armed || _done) return;
            float now = Time.realtimeSinceStartup;
            // Not in a mission yet (menu/loading): DynamicMap only exists in the GameWorld scene. Hold the clock.
            if (Map() == null) { _firstTickAt = -1f; return; }
            if (_firstTickAt < 0f) { _firstTickAt = now; _stateAt = now; Trace($"first-tick (in-mission) map=True"); }
            if (now - _firstTickAt < WarmupSec) return; // warm-up: let load+join+brain settle
            if (!_beganLogged) { _beganLogged = true; Trace($"warmup-done begin shots, map={(Map() != null)}"); }

            try
            {
                switch (_step)
                {
                    case Step.Acting:
                        Trace($"act i={_i} name={_shots[_i].Name}");
                        try { _shots[_i].Action?.Invoke(); }
                        catch (Exception e) { Trace($"action-failed {_shots[_i].Name} {e.Message}"); _log.LogWarning($"[NUCLEUS:SHOT] action-failed name={_shots[_i].Name} {e.Message}"); }
                        _stateAt = now; _step = Step.Settling;
                        break;

                    case Step.Settling:
                        if (now - _stateAt >= _shots[_i].SettleSec) { _step = Step.Capturing; }
                        break;

                    case Step.Capturing:
                        _pendingFile = Path.Combine(_dir, _shots[_i].Name + ".png");
                        try { if (File.Exists(_pendingFile)) File.Delete(_pendingFile); } catch { }
                        Trace($"capture call {_pendingFile}");
                        ScreenCapture.CaptureScreenshot(_pendingFile, 1);
                        _stateAt = now; _step = Step.AwaitingFile;
                        break;

                    case Step.AwaitingFile:
                        long len = 0;
                        try { if (File.Exists(_pendingFile)) len = new FileInfo(_pendingFile).Length; } catch { }
                        if (len > 0)
                        {
                            _captured++;
                            Trace($"shot-ok {_shots[_i].Name} bytes={len}");
                            _log.LogInfo($"[NUCLEUS:SHOT] name={_shots[_i].Name} file={_pendingFile} bytes={len}");
                            _step = Step.Advance;
                        }
                        else if (now - _stateAt >= FileWaitSec)
                        {
                            Trace($"shot-timeout {_shots[_i].Name}");
                            _log.LogWarning($"[NUCLEUS:SHOT] timeout name={_shots[_i].Name} file={_pendingFile}");
                            _step = Step.Advance;
                        }
                        break;

                    case Step.Advance:
                        _i++;
                        if (_i >= _shots.Count)
                        {
                            _done = true;
                            Trace($"complete count={_captured}");
                            _log.LogInfo($"[NUCLEUS:SELFTEST] PASS shots-complete count={_captured}");
                        }
                        else { _stateAt = now; _step = Step.Acting; }
                        break;
                }
            }
            catch (Exception e)
            {
                Trace("shots-exception " + e);
                _log.LogError("[NUCLEUS:SELFTEST] FAIL shots-exception " + e);
                _done = true;
            }
        }

        private static DynamicMap Map() => SceneSingleton<DynamicMap>.i;

        private static string _openBezel;

        // Open one panel exclusively: toggle the previously-opened one off first so panels don't overlap.
        private static void OpenPanel(string modId)
        {
            if (_openBezel != null && _openBezel != modId) PressBezel(_openBezel);
            PressBezel(modId);
            _openBezel = modId;
        }

        // Open a mod panel by invoking its native bezel button (HostButtons names them "NucleusBezel_<modId>").
        private static void PressBezel(string modId)
        {
            var name = "NucleusBezel_" + modId;
            var btn = FindButton(name);
            if (btn == null) { _log.LogWarning($"[NUCLEUS:SHOT] bezel-not-found {name}"); return; }
            btn.onClick.Invoke();
        }

        // Click the first "SELECT" button in any open panel so an objective becomes selected (drives the
        // selected-objective map detail). Matches on the button's TMP label text.
        private static void ClickFirstSelect()
        {
            foreach (var b in Resources.FindObjectsOfTypeAll<Button>())
            {
                if (b == null || !b.isActiveAndEnabled) continue;
                var lbl = b.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                if (lbl != null && lbl.text != null && lbl.text.Trim() == "SELECT")
                {
                    b.onClick.Invoke();
                    Trace("clicked SELECT");
                    return;
                }
            }
            Trace("no SELECT button found");
        }

        private static Button FindButton(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) { var b = go.GetComponent<Button>(); if (b != null) return b; }
            // Fallback: scan all loaded buttons (incl. inactive) by name.
            foreach (var b in Resources.FindObjectsOfTypeAll<Button>())
                if (b != null && b.name == name) return b;
            return null;
        }
    }
}
