using System.Collections.Generic;
using CommanderLayer.Core.Model;
using CommanderLayer.Core.Ports;
using CommanderLayer.Game;
using CommanderLayer.Patches;
using CommanderLayer.Ui;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CommanderLayer.Composition
{
    /// <summary>
    /// Composition root: builds the commander service (planner + adapters), hosts the modal on its own
    /// screen-space canvas (opened by the native CMD bezel button), draws the order overlay on the map's
    /// icon layer, routes map clicks to order placement, and runs the throttled management loop. Driven by
    /// the DynamicMap.Update Harmony postfix (this game doesn't pump a MonoBehaviour Update on mod objects).
    /// </summary>
    public sealed class CommanderRuntime
    {
        private readonly IPlayerContext _player;
        private readonly IMapProjection _projection;
        private readonly CommanderService _service;

        private Canvas _canvas;
        private CommanderMapScreen _screen;
        private MapOverlay _overlay;
        private Theme _theme;
        private DynamicMap _lastMap;
        private Button _cmdButton;
        private OrderKind? _armed;
        private bool _firstTick = true;
        private float _nextManage;
        private GUIStyle _fallbackStyle;

        public CommanderRuntime()
        {
            _player = new GamePlayerContext();
            _projection = new DynamicMapProjection();
            _service = new CommanderService(new CommanderConfig { ArriveRadius = Plugin.ArriveRadius });
            Plugin.Log?.LogInfo("CommanderRuntime constructed.");
        }

        public void Tick()
        {
            if (_firstTick)
            {
                _firstTick = false;
                Plugin.Log?.LogInfo("CommanderRuntime first Tick — driver alive.");
            }

            EnsureCanvas();
            EnsureScreen();

            var map = SceneSingleton<DynamicMap>.i;
            bool open = map != null && DynamicMap.mapMaximized;

            if (map != null && !ReferenceEquals(map, _lastMap))
            {
                _lastMap = map;
                _overlay = map.iconLayer != null ? new MapOverlay(map.iconLayer.transform, _projection) : null;
            }
            if (map == null) { _lastMap = null; _overlay = null; }

            if (_canvas != null) _canvas.enabled = open;
            if (!open) return;

            // Place: armed order kind + left-click on the map (not on our panel).
            if (_armed.HasValue && Input.GetMouseButtonDown(0) && !IsPointerOverUi()
                && _projection.TryCursorToWorld(out var world))
            {
                var state = _service.PlaceOrder(_armed.Value, world);
                Plugin.Log?.LogInfo($"Placed {state.Order.Kind} order: {state.Summary}");
                _armed = null;
            }

            // Throttled management loop.
            if (Time.unscaledTime >= _nextManage)
            {
                _nextManage = Time.unscaledTime + _service.Config.ManagementIntervalSeconds;
                _service.Tick();
            }

            _player.TryGetLocalFaction(out var faction);
            _screen?.Render(_service.Orders, faction, _armed);
            _overlay?.Render(_service.Orders, PositionsById());
        }

        private Dictionary<string, Vec3> PositionsById()
        {
            var dict = new Dictionary<string, Vec3>();
            foreach (var u in _service.LastRoster) dict[u.Id] = u.Position;
            return dict;
        }

        // Commandeer a blank MFD bezel button on map-open → "CMD" that toggles the modal.
        public void AttachCmdButton(VirtualMFD mfd)
        {
            if (mfd == null || _cmdButton != null) return;
            EnsureCanvas();
            EnsureScreen();

            var rightButtons = AccessTools.Field(typeof(VirtualMFD), "rightButtons").GetValue(mfd) as List<Button>;
            var rightScreens = AccessTools.Field(typeof(VirtualMFD), "rightScreens").GetValue(mfd) as List<MFDScreen>;
            var leftButtons = AccessTools.Field(typeof(VirtualMFD), "leftButtons").GetValue(mfd) as List<Button>;
            var leftScreens = AccessTools.Field(typeof(VirtualMFD), "leftScreens").GetValue(mfd) as List<MFDScreen>;

            var btn = FindBlankButton(rightButtons, rightScreens) ?? FindBlankButton(leftButtons, leftScreens);
            if (btn == null) { Plugin.Log?.LogWarning("No blank MFD bezel button available for CMD."); return; }

            btn.enabled = true;
            btn.gameObject.SetActive(true);
            var txt = btn.GetComponentInChildren<Text>(includeInactive: true);
            if (txt != null) { txt.text = "CMD"; txt.enabled = true; txt.gameObject.SetActive(true); }
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => _screen?.Toggle());
            _cmdButton = btn;
            Plugin.Log?.LogInfo($"CMD button attached (label set={txt != null}).");
        }

        private static Button FindBlankButton(List<Button> buttons, List<MFDScreen> screens)
        {
            if (buttons == null) return null;
            for (int i = 0; i < buttons.Count; i++)
            {
                var b = buttons[i];
                if (b == null) continue;
                bool hasScreen = screens != null && i < screens.Count && screens[i] != null;
                if (!hasScreen) return b;
            }
            return null;
        }

        private void EnsureCanvas()
        {
            if (_canvas != null) return;
            var go = new GameObject("CommanderCanvas");
            Object.DontDestroyOnLoad(go);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 5000;
            go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            go.AddComponent<GraphicRaycaster>();
            _canvas.enabled = false;
            Plugin.Log?.LogInfo("Commander canvas created.");
        }

        private void EnsureScreen()
        {
            if (_screen != null || _canvas == null) return;
            _player.TryGetLocalFaction(out var faction);
            _theme = Theme.FromFaction(faction);
            _screen = new CommanderMapScreen(_canvas.transform, _theme,
                onArm: k => _armed = k,
                onClearAll: () => _service.ClearAll());
            Plugin.Log?.LogInfo("Commander panel built.");
        }

        private static bool IsPointerOverUi()
            => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        public void DrawMenuFallback()
        {
            if (MainMenuBadgePatch.Created || MissionManager.i != null) return;
            if (_fallbackStyle == null)
            {
                _fallbackStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
                _fallbackStyle.normal.textColor = new Color(0.4f, 0.8f, 1f);
            }
            GUI.Label(new Rect(16f, 12f, 420f, 24f), $"Commander mod loaded  v{Plugin.Version}", _fallbackStyle);
        }
    }
}
