using System.Collections.Generic;
using Nucleus.Core.Model;
using Nucleus.Core.Ports;
using Nucleus.Game;
using Nucleus.Ui;
using Nucleus.Ui.Native;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nucleus.Composition
{
    /// <summary>
    /// Composition root: builds the commander service, renders the Commander panel INTO a native MFD screen
    /// the host provides (so the game owns the window's placement, green highlight, and close-on-map-minimize),
    /// draws the order overlay + live placement ring on the map's icon layer, routes clicks to order placement,
    /// runs the throttled management loop. Driven by the DynamicMap.Update Harmony postfix.
    /// </summary>
    public sealed class CommanderRuntime
    {
        private readonly IPlayerContext _player;
        private readonly IMapProjection _projection;
        private readonly CommanderService _service;

        private CommanderPanel _panel;
        private MapOverlay _overlay;
        private Theme _theme;
        private DynamicMap _lastMap;
        private string _dragObjId;                 // objective being dragged (mouse held on its marker)
        private Vec3 _pressWorld;                   // cursor world at mousedown — drag only starts past a dead-zone
        private bool _dragging;                     // true once the cursor moved beyond the dead-zone
        private Nucleus.Core.Command.HqSnapshot _lastHq;
        private FlightHud _hud;                      // bottom-right objective HUD shown while flying (map closed)
        private bool _hudVisible = true;
        private bool _firstTick = true;
        private bool _loggedPanel;
        private float _nextManage;
        private float _nextRender;
        // The heavy projection (AutoHq) + overlay/panel render run at this rate, not every frame — clicks/drag
        // stay per-frame for responsiveness, but re-rendering the whole map+panel each frame over hundreds of
        // units was the in-mission lag. ~7 Hz is smooth enough for strategic markers.
        private const float RenderIntervalSeconds = 0.14f;

        public CommanderRuntime()
        {
            _player = new GamePlayerContext();
            _projection = new DynamicMapProjection();
            _service = new CommanderService(new CommanderConfig());
            CommanderPlugin.Log?.LogInfo("CommanderRuntime constructed.");
        }

        /// <summary>The shared live campaign — the Commander mod publishes this to the host so Build/Squad/
        /// Warfare render their slices of the same state.</summary>
        public Nucleus.Core.Command.ICampaign Campaign => _service;

        /// <summary>
        /// Build the Commander panel into the host-provided native MFD-screen content RectTransform. Called
        /// once by the host when the map's bezel screens are created. The native MFDScreen shows/hides this;
        /// our content stays active inside it.
        /// </summary>
        public void BuildPanel(RectTransform parent)
        {
            if (_panel != null || parent == null) return;
            CaptureNativeButtonSprite();
            CaptureNativeAssets();
            _player.TryGetLocalFaction(out var faction);
            _theme = Theme.FromFaction(faction);
            // CMD screen = objective management + the two command toggles — built the SAME way as every other
            // mod (a CommanderPanel filling the host's standard ModPanel chrome). Build/Squad/Warfare own the
            // rest. Everything is objectives now: pick a kind, click the map to drop, select a marker to edit.
            _panel = new CommanderPanel(parent, _theme,
                onSetAiCommander: on => _service.SetAiCreatesObjectives(on),
                onSetAutoFill: on => _service.SetAiAutoFill(on),
                onToggleOpManual: id => _service.ToggleOperationManual(id),
                onToggleSquadManual: id => _service.ToggleSquadManual(id),
                onBuyConvoy: name => _service.BuyConvoy(name),
                onArmObjective: _ => { },                       // arming is panel-side state; runtime reads ArmedObjective
                onSelectObjective: id => _dragObjId = null,     // panel selected via row click; no drag from the list
                onRemoveObjective: id => _service.RemoveObjective(id),
                onNudgePriority: NudgePriority,
                onCycleKind: CycleKind,
                onAssignSquad: (objId, squadId) => _service.AssignSquad(objId, squadId),
                sections: CommanderPanel.PanelSections.Objectives | CommanderPanel.PanelSections.Mode
                    | CommanderPanel.PanelSections.Feed); // show the AI's narrated decisions on the command screen
            UiFactory.Stretch(_panel.Root);
            CommanderPlugin.Log?.LogInfo("Commander panel built into native MFD screen.");
        }

        public void Tick()
        {
            if (_firstTick) { _firstTick = false; CommanderPlugin.Log?.LogInfo("CommanderRuntime first Tick — driver alive."); }

            var map = SceneSingleton<DynamicMap>.i;
            bool open = map != null && DynamicMap.mapMaximized;

            if (map != null && !ReferenceEquals(map, _lastMap))
            {
                _lastMap = map;
                _overlay = map.iconLayer != null ? new MapOverlay(map.iconLayer.transform, _projection) : null;
            }
            if (map == null) { _lastMap = null; _overlay = null; }

            // The autonomous commander runs whether or not the panel is visible (throttled).
            if (Time.unscaledTime >= _nextManage)
            {
                _nextManage = Time.unscaledTime + _service.Config.ManagementIntervalSeconds;
                _service.Tick();
            }

            // Input is handled every frame so clicks/drag stay responsive.
            if (!open) { _overlay?.Clear(); _dragObjId = null; }
            else if (_panel != null) HandleMapInteraction();

            // The heavy projection + overlay/panel render is throttled (was every frame -> in-mission lag).
            if (Time.unscaledTime >= _nextRender)
            {
                _nextRender = Time.unscaledTime + RenderIntervalSeconds;
                _lastHq = _service.AutoHq();
                if (open && _panel != null)
                    _overlay?.RenderObjectives(_lastHq?.Operations, _panel.SelectedObjectiveId,
                        _lastHq?.Squads, PositionsById());

                if (_panel != null)
                {
                    _panel.RenderObjectives(_lastHq);
                    _panel.RenderHq(_lastHq, _service.BuildCatalog(), _service.Funds());
                    if (!_loggedPanel) { _loggedPanel = true; CommanderPlugin.Log?.LogInfo("[panel] Commander panel rendering."); }
                }
            }
        }

        private float _nextHud;

        /// <summary>
        /// Render the in-flight objective HUD. Driven by a MissionManager.Update patch (NOT the DynamicMap.Update
        /// tick that drives the rest of this runtime) because that one stops firing while the map is closed —
        /// exactly when the flight HUD must be visible. Self-contained: lazy-builds the widget on a screen-space
        /// canvas, refreshes the Hq snapshot on its own throttle, shows while flying, hides while the map is open.
        /// </summary>
        public void TickHud()
        {
            if (!CommanderPlugin.ShowFlightHud) { _hud?.SetVisible(false); return; }

            if (_hud == null)
            {
                var canvas = FindOverlayCanvas();
                if (canvas == null) return;
                _hud = new FlightHud(canvas.transform);
                _hudVisible = true;
            }
            if (Input.GetKeyDown(CommanderPlugin.HudToggleKey)) _hudVisible = !_hudVisible;

            var map = SceneSingleton<DynamicMap>.i;
            bool open = map != null && DynamicMap.mapMaximized;
            if (open || !_hudVisible) { _hud.SetVisible(false); return; }

            if (Time.unscaledTime >= _nextHud)
            {
                _nextHud = Time.unscaledTime + 0.5f;     // cheap: refresh the snapshot a couple times a second
                _lastHq = _service.AutoHq();
                _hud.Render(_lastHq);
            }
        }

        // The active top-most screen-space overlay canvas (same pick as the host's menu/setup widgets).
        private static Canvas FindOverlayCanvas()
        {
            Canvas best = null;
            foreach (var c in Object.FindObjectsOfType<Canvas>())
            {
                if (c == null || !c.isActiveAndEnabled) continue;
                if (c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                if (best == null || c.sortingOrder > best.sortingOrder) best = c;
            }
            return best;
        }

        // The drop-then-edit-in-place flow: with a kind armed, click drops a new objective; otherwise a click
        // selects the nearest marker and a held drag moves it. All routed through the objective CRUD.
        private void HandleMapInteraction()
        {
            if (IsPointerOverUi()) { if (Input.GetMouseButtonUp(0)) _dragObjId = null; return; }
            if (!_projection.TryCursorToWorld(out var cursor)) return;

            var armed = _panel.ArmedObjective;
            if (Input.GetMouseButtonDown(0))
            {
                if (armed.HasValue)
                {
                    var id = _service.CreateObjective(armed.Value, cursor);
                    _panel.ClearArmedObjective();
                    _panel.SetSelectedObjective(id);
                    CommanderPlugin.Log?.LogInfo($"[obj] dropped {armed.Value} -> {id}");
                }
                else
                {
                    // Select the nearest objective marker (within a screen-constant radius) and arm a drag.
                    var hit = NearestObjective(cursor, 18f);
                    if (hit != null) { _panel.SetSelectedObjective(hit); _dragObjId = hit; _pressWorld = cursor; _dragging = false; }
                }
            }
            else if (Input.GetMouseButton(0) && _dragObjId != null)
            {
                // Dead-zone: a plain click only selects; moving past the threshold begins drag-to-move so a
                // stationary select-click never yanks the objective to the cursor.
                if (!_dragging && _pressWorld.HorizontalDistanceTo(cursor) > DragDeadZoneMeters) _dragging = true;
                if (_dragging) _service.MoveObjective(_dragObjId, cursor); // operation shares the objective ref
            }
            else if (Input.GetMouseButtonUp(0))
            {
                _dragObjId = null;
                _dragging = false;
            }
        }

        // Cursor must travel this far (world meters) from the press point before a select becomes a move.
        private const float DragDeadZoneMeters = 250f;

        // Nearest live objective to a world point, in screen-constant map-local units (null if none within max).
        private string NearestObjective(Vec3 cursorWorld, float maxLocal)
        {
            var ops = _lastHq?.Operations;
            if (ops == null) return null;
            var cl = _projection.WorldToMapLocal(cursorWorld);
            string best = null; float bestD = maxLocal;
            foreach (var op in ops)
            {
                // Only select markers the overlay actually draws (it skips Complete/Failed).
                if (op.Status == Nucleus.Core.Command.OperationStatus.Complete
                    || op.Status == Nucleus.Core.Command.OperationStatus.Failed) continue;
                var l = _projection.WorldToMapLocal(op.Position);
                float dx = l.X - cl.X, dy = l.Y - cl.Y;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < bestD) { bestD = d; best = op.ObjectiveId; }
            }
            return best;
        }

        private void NudgePriority(string id, int delta)
        {
            if (!TryFindOp(id, out var op)) return;
            float p = Mathf.Clamp(op.Priority + delta * 0.5f, 0.5f, 9.5f);
            _service.EditObjective(id, priority: p);
        }

        private void CycleKind(string id)
        {
            if (!TryFindOp(id, out var op)) return;
            var values = (Nucleus.Core.Command.ObjectiveKind[])System.Enum.GetValues(typeof(Nucleus.Core.Command.ObjectiveKind));
            int next = (System.Array.IndexOf(values, op.Kind) + 1) % values.Length;
            _service.EditObjective(id, kind: values[next]);
        }

        private bool TryFindOp(string objectiveId, out Nucleus.Core.Command.OperationView op)
        {
            op = default;
            var ops = _lastHq?.Operations;
            if (ops == null) return false;
            foreach (var o in ops) if (o.ObjectiveId == objectiveId) { op = o; return true; }
            return false;
        }

        private Dictionary<string, Vec3> _posCache;
        private object _posCacheKey;

        // Unit-id -> position map for the map overlay. Rebuilt only when the roster list instance changes (the
        // throttled 3s management tick swaps in a new list), not on every render frame over ~400 units.
        private Dictionary<string, Vec3> PositionsById()
        {
            var roster = _service.LastRoster;
            if (ReferenceEquals(_posCacheKey, roster) && _posCache != null) return _posCache;
            var dict = new Dictionary<string, Vec3>(roster.Count);
            foreach (var u in roster) dict[u.Id] = u.Position;
            _posCacheKey = roster;
            _posCache = dict;
            return dict;
        }

        private static bool IsPointerOverUi()
            => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        // Capture the game's own visual resources (font, HUD colors, map/threat icons) from the single
        // codegen'd source of truth — NativeAssets, a typed snapshot of GameAssets — and mirror them into
        // the Ui-layer caches so our labels/cues/icons read as native. One read point; no scattered
        // GameAssets reads, no hardcoded/duplicated values. Drift in any asset fails the contract test.
        private static void CaptureNativeAssets()
        {
            var assets = Nucleus.Game.Generated.NativeAssets.Capture();
            if (assets == null) return;

            if (assets.playerNameFont != null)
            {
                UiFactory.Font = assets.playerNameFont;
                CommanderPlugin.Log?.LogInfo("Using native game font (GameAssets.playerNameFont).");
            }
            if (!NativeColors.Captured)
            {
                NativeColors.Friendly = assets.HUDFriendly;
                NativeColors.Hostile = assets.HUDHostile;
                NativeColors.Neutral = assets.HUDNeutral;
                NativeColors.Captured = true;
            }
            if (!NativeIcons.Captured)
            {
                NativeIcons.Airbase = assets.airbaseSprite;
                NativeIcons.EnemyContact = assets.targetUnitSprite;
                NativeIcons.FriendlyContact = assets.targetUnitSpriteFriendly;
                NativeIcons.MissileWarning = assets.missileWarningSprite;
                NativeIcons.Warhead = assets.warheadSprite;
                NativeIcons.Captured = true;
            }
        }

        // Borrow a sliced sprite from a real game UI button so our panel buttons match the game's look.
        // The harvest idiom lives in the UI lib (NativeUi) — the app just consumes it.
        private static void CaptureNativeButtonSprite()
        {
            if (UiFactory.ButtonSprite != null) return;
            UiFactory.ButtonSprite = NativeUi.SlicedButtonSprite();
        }
    }
}
