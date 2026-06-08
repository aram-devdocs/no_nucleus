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
    /// <summary>Composition root: builds the commander service, renders the panel into the host's native MFD
    /// screen, draws the map overlay, routes map clicks to objective CRUD, and runs the throttled management
    /// loop. Driven by the DynamicMap.Update Harmony postfix.</summary>
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
        private WorldMarkerLayer _worldMarkers;      // world-anchored objective rings out the canopy (map closed)
        private CommandBanner _banner;               // one-time "AI is commanding your side" notice (map closed)
        private bool _bannerDismissed;               // UI-local sticky flag: dismissed for the session
        private bool _hudVisible = true;
        private bool _firstTick = true;
        private bool _loggedPanel;
        private float _nextManage;
        private float _nextRender;
        // Heavy projection + render run at ~7 Hz, not per-frame (re-rendering map+panel over hundreds of units
        // each frame was the in-mission lag); clicks/drag stay per-frame for responsiveness.
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

        /// <summary>The one UI-local objective selection shared by the panel's order tree and the map overlay
        /// (bidirectional): the map reads it to highlight + draw the selection ring, and both panel rows and map
        /// clicks write it. Backed by the panel's selection so there is a single source of truth.</summary>
        public string SelectedObjectiveId
        {
            get => _panel?.SelectedObjectiveId;
            set => _panel?.SetSelectedObjective(value);
        }

        /// <summary>Build the Commander panel into the host-provided native MFD-screen content. Called once when
        /// the map's bezel screens are created; the native MFDScreen shows/hides it.</summary>
        public void BuildPanel(RectTransform parent)
        {
            if (_panel != null || parent == null) return;
            CaptureNativeButtonSprite();
            CaptureNativeAssets();
            _player.TryGetLocalFaction(out var faction);
            _theme = Theme.FromFaction(faction);
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
                onToggleOrderManual: ToggleOrderManual,        // take over / release the order owning the selected node
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
                _overlay = map.iconLayer != null ? new MapOverlay(map.iconLayer.transform, _projection, _theme ?? Theme.Default) : null;
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
                    _overlay?.RenderObjectives(_lastHq?.Orders, _panel.SelectedObjectiveId, PositionsById());

                if (_panel != null)
                {
                    _panel.RenderObjectives(_lastHq);
                    _panel.RenderHq(_lastHq, _service.BuildCatalog(), _service.Funds());
                    if (!_loggedPanel) { _loggedPanel = true; CommanderPlugin.Log?.LogInfo("[panel] Commander panel rendering."); }
                }
            }
        }

        private float _nextHud;

        /// <summary>Render the in-flight objective HUD. Driven by a MissionManager.Update patch — NOT the
        /// DynamicMap.Update tick, which stops firing while the map is closed, exactly when the HUD must show.
        /// Lazy-builds on a screen-space canvas, hides while the map is open.</summary>
        public void TickHud()
        {
            // The master AI dial + its notice run every frame (this is the reliable per-frame hook), independent
            // of the flight-HUD toggle — the key must work and the banner must show even with the HUD hidden.
            HandleMasterAiKey();
            TickBanner();

            if (!CommanderPlugin.ShowFlightHud) { _hud?.SetVisible(false); return; }

            if (_hud == null)
            {
                var canvas = FindOverlayCanvas();
                if (canvas == null) return;
                _hud = new FlightHud(canvas.transform, _theme ?? Theme.Default);
                _worldMarkers = new WorldMarkerLayer(canvas.transform, _theme ?? Theme.Default);
                _hudVisible = true;
            }
            if (Input.GetKeyDown(CommanderPlugin.HudToggleKey)) _hudVisible = !_hudVisible;

            var map = SceneSingleton<DynamicMap>.i;
            bool open = map != null && DynamicMap.mapMaximized;
            if (open || !_hudVisible) { _hud.SetVisible(false); _worldMarkers.SetVisible(false); return; }

            if (Time.unscaledTime >= _nextHud)
            {
                _nextHud = Time.unscaledTime + 0.5f;     // cheap: refresh the snapshot a couple times a second
                _lastHq = _service.AutoHq();
                _hud.Render(_lastHq);
            }
            // World markers re-project EVERY frame (cached snapshot) so they track the aircraft smoothly.
            _worldMarkers.Render(_lastHq);
        }

        // The master AI dial: one key flips BOTH command toggles for the player's side. Off freezes new orders +
        // auto-fill (running operations finish on their own); on hands the whole side back to the AI commander.
        private void HandleMasterAiKey()
        {
            if (!Input.GetKeyDown(CommanderPlugin.MasterAiKey)) return;
            bool next = !(_service.AiCreatesObjectives || _service.AiAutoFill);
            _service.SetAiCreatesObjectives(next);
            _service.SetAiAutoFill(next);
            CommanderPlugin.Log?.LogInfo($"[NUCLEUS:SELFTEST] PASS master-ai-toggle on={next}");
        }

        // The one-time "AI is commanding your side" notice, shown while flying (map closed) when the AI holds the
        // side, until the player dismisses it (UI-local sticky flag). Lazy-built on the overlay canvas.
        private void TickBanner()
        {
            bool aiCommanding = _service.AiCreatesObjectives || _service.AiAutoFill;
            if (!aiCommanding || _bannerDismissed) { _banner?.SetVisible(false); return; }

            if (_banner == null)
            {
                var canvas = FindOverlayCanvas();
                if (canvas == null) return;
                _banner = new CommandBanner(canvas.transform, _theme ?? Theme.Default, () => _bannerDismissed = true);
            }

            var map = SceneSingleton<DynamicMap>.i;
            bool open = map != null && DynamicMap.mapMaximized;
            _banner.SetVisible(!open); // hide over the map — the command panel already shows the AI state there
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

        // Nearest selectable marker to a world point, in screen-constant map-local units (null if none within
        // max). Mirrors EXACTLY what MapOverlay draws — order parents (the goal), plus the child nodes of the
        // currently-selected order — so a click only ever picks a marker the player can actually see.
        private string NearestObjective(Vec3 cursorWorld, float maxLocal)
        {
            var orders = _lastHq?.Orders;
            if (orders == null) return null;
            var cl = _projection.WorldToMapLocal(cursorWorld);
            string sel = _panel?.SelectedObjectiveId;
            string best = null; float bestD = maxLocal;
            int shown = 0;
            foreach (var ord in orders)
            {
                if (ord.Status != Nucleus.Core.Command.OrderStatus.Active) continue;
                if (shown >= MapOverlay.MaxOrderMarkers) break;
                shown++;
                ConsiderMarker(ord.GoalObjectiveId, MapOverlay.GoalPosition(ord), cl, ref best, ref bestD);

                bool expanded = ord.GoalObjectiveId == sel || NodeSelected(ord, sel);
                if (!expanded || ord.Nodes == null) continue;
                foreach (var n in ord.Nodes)
                    if (!n.IsGoal) ConsiderMarker(n.ObjectiveId, n.Position, cl, ref best, ref bestD);
            }
            return best;
        }

        private void ConsiderMarker(string id, Vec3 worldPos, Vec3 cursorLocal, ref string best, ref float bestD)
        {
            var l = _projection.WorldToMapLocal(worldPos);
            float dx = l.X - cursorLocal.X, dy = l.Y - cursorLocal.Y;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d < bestD) { bestD = d; best = id; }
        }

        private static bool NodeSelected(Nucleus.Core.Command.OrderView ord, string selectedId)
        {
            if (selectedId == null || ord.Nodes == null) return false;
            foreach (var n in ord.Nodes) if (!n.IsGoal && n.ObjectiveId == selectedId) return true;
            return false;
        }

        // The detail pane addresses a node by its objective id; resolve it to the owning order and take that whole
        // order over (or release it) — the AI yields/reclaims its tree. Goal rows match by GoalObjectiveId.
        private void ToggleOrderManual(string objectiveId)
        {
            var orders = _lastHq?.Orders;
            if (orders == null || objectiveId == null) return;
            foreach (var o in orders)
            {
                if (o.GoalObjectiveId == objectiveId) { _service.ToggleOrderManual(o.Id); return; }
                if (o.Nodes == null) continue;
                foreach (var n in o.Nodes)
                    if (n.ObjectiveId == objectiveId) { _service.ToggleOrderManual(o.Id); return; }
            }
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

        // Mirror the game's font/HUD-colors/icons from the codegen'd NativeAssets snapshot into the Ui-layer
        // caches, so our labels/cues read as native. One read point; asset drift fails the contract test.
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
