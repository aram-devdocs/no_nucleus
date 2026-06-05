using System.Collections.Generic;
using CommanderLayer.Core.Controller;
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
    /// Composition root: builds the adapter graph + controller, hosts the Commander panel on its own
    /// screen-space canvas (shown while the map is open), draws the marker overlay on the map's icon
    /// layer, and routes map clicks to placement. Driven by Plugin.Update()/OnGUI() (a BepInEx
    /// MonoBehaviour Unity reliably pumps), so it does not depend on a custom GameObject being ticked.
    /// </summary>
    public sealed class CommanderRuntime
    {
        private readonly IPlayerContext _player;
        private readonly IUnitQuery _units;
        private readonly IObjectiveService _objectives;
        private readonly IMapProjection _projection;
        private readonly IClock _clock;
        private readonly CommanderController _controller;

        private Canvas _canvas;
        private CommanderMapScreen _screen;
        private MapOverlay _overlay;
        private Theme _theme;
        private DynamicMap _lastMap;
        private Button _cmdButton;
        private bool _wasOpen;
        private bool _firstTick = true;
        private float _nextRefresh;
        private float _nextHeartbeat;
        private GUIStyle _fallbackStyle;

        public CommanderRuntime()
        {
            _player = new GamePlayerContext();
            _units = new GameUnitQuery();
            _objectives = new GameObjectiveService();
            _projection = new DynamicMapProjection();
            _clock = new UnityClock();
            _controller = new CommanderController(_player, _units, _objectives, _clock, Plugin.ArriveRadius);
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

            // Heartbeat so we can see map state even when nothing else changes.
            if (_clock.Now >= _nextHeartbeat)
            {
                _nextHeartbeat = _clock.Now + 3f;
                Plugin.Log?.LogInfo($"[hb] DynamicMap={(map != null)} maximized={(map != null && DynamicMap.mapMaximized)} canvas={(_canvas != null)} screen={(_screen != null)} overlay={(_overlay != null)}");
            }

            if (open != _wasOpen)
            {
                _wasOpen = open;
                Plugin.Log?.LogInfo($"Map {(open ? "OPENED" : "closed")}.");
            }

            if (map != null)
            {
                if (!ReferenceEquals(map, _lastMap))
                {
                    _overlay = null;
                    _lastMap = map;
                    Plugin.Log?.LogInfo($"DynamicMap found. iconLayer={(map.iconLayer != null)}.");
                }
                if (_overlay == null && map.iconLayer != null)
                {
                    _overlay = new MapOverlay(map.iconLayer.transform, _theme, _projection);
                    Plugin.Log?.LogInfo("Map overlay built.");
                }
            }
            else
            {
                _overlay = null;
                _lastMap = null;
            }

            if (_canvas != null)
            {
                _canvas.enabled = open;
            }
            if (!open)
            {
                return;
            }

            if (Plugin.ArmKey != KeyCode.None && Input.GetKeyDown(Plugin.ArmKey))
            {
                _controller.ArmPlacement();
            }

            if (_controller.State.PlacementArmed
                && Input.GetMouseButtonDown(0)
                && !IsPointerOverUi()
                && _projection.TryCursorToWorld(out var world))
            {
                if (_controller.TryPlaceAt(world))
                {
                    Plugin.Log?.LogInfo($"Objective placed at {world}.");
                }
            }

            if (_clock.Now >= _nextRefresh)
            {
                _nextRefresh = _clock.Now + 0.5f;
                _controller.Refresh();
            }

            _screen?.Render(_controller.State);
            _overlay?.Render(_controller.State);
        }

        private void EnsureCanvas()
        {
            if (_canvas != null)
            {
                return;
            }
            var go = new GameObject("CommanderCanvas");
            Object.DontDestroyOnLoad(go);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 5000;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            go.AddComponent<GraphicRaycaster>();
            _canvas.enabled = false;
            Plugin.Log?.LogInfo("Commander canvas created.");
        }

        private void EnsureScreen()
        {
            if (_screen != null || _canvas == null)
            {
                return;
            }
            _player.TryGetLocalFaction(out var faction);
            _theme = Theme.FromFaction(faction);
            _screen = new CommanderMapScreen(
                _canvas.transform,
                _theme,
                onArmPlace: () => _controller.ArmPlacement(),
                onClear: () => _controller.Clear());
            Plugin.Log?.LogInfo("Commander panel built.");
        }

        // Commandeer a blank MFD bezel button on the map (a VirtualMFD button with no assigned screen,
        // left disabled by SetupButtons) and turn it into "CMD" that opens the Commander panel. Called from
        // the VirtualMFD_onMapMaximized Harmony postfix. leftButtons/rightButtons are private, so reflection.
        public void AttachCmdButton(VirtualMFD mfd)
        {
            if (mfd == null || _cmdButton != null)
            {
                return; // already attached this session (Unity == treats a destroyed button as null on reload)
            }
            EnsureCanvas();
            EnsureScreen();

            var rightButtons = AccessTools.Field(typeof(VirtualMFD), "rightButtons").GetValue(mfd) as List<Button>;
            var rightScreens = AccessTools.Field(typeof(VirtualMFD), "rightScreens").GetValue(mfd) as List<MFDScreen>;
            var leftButtons = AccessTools.Field(typeof(VirtualMFD), "leftButtons").GetValue(mfd) as List<Button>;
            var leftScreens = AccessTools.Field(typeof(VirtualMFD), "leftScreens").GetValue(mfd) as List<MFDScreen>;

            var btn = FindBlankButton(rightButtons, rightScreens) ?? FindBlankButton(leftButtons, leftScreens);
            if (btn == null)
            {
                Plugin.Log?.LogWarning("No blank MFD bezel button available for CMD.");
                return;
            }

            btn.enabled = true;
            btn.gameObject.SetActive(true);
            var txt = btn.GetComponentInChildren<Text>(includeInactive: true);
            if (txt != null)
            {
                txt.text = "CMD";
                txt.enabled = true;
                txt.gameObject.SetActive(true);
            }
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => _screen?.Toggle());
            _cmdButton = btn;
            Plugin.Log?.LogInfo($"CMD button attached (label set={txt != null}).");
        }

        // A blank bezel slot = a button index with no corresponding (non-null) screen.
        private static Button FindBlankButton(List<Button> buttons, List<MFDScreen> screens)
        {
            if (buttons == null)
            {
                return null;
            }
            for (int i = 0; i < buttons.Count; i++)
            {
                var b = buttons[i];
                if (b == null)
                {
                    continue;
                }
                bool hasScreen = screens != null && i < screens.Count && screens[i] != null;
                if (!hasScreen)
                {
                    return b;
                }
            }
            return null;
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        /// <summary>IMGUI fallback (called from Plugin.OnGUI): confirms load while at the menu.</summary>
        public void DrawMenuFallback()
        {
            if (MainMenuBadgePatch.Created || MissionManager.i != null)
            {
                return;
            }
            if (_fallbackStyle == null)
            {
                _fallbackStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
                _fallbackStyle.normal.textColor = new Color(0.4f, 0.8f, 1f);
            }
            GUI.Label(new Rect(16f, 12f, 420f, 24f), $"Commander mod loaded  v{Plugin.Version}", _fallbackStyle);
        }
    }
}
