using System.Collections.Generic;
using CommanderLayer.Core.Model;
using CommanderLayer.Core.Ports;
using CommanderLayer.Game;
using CommanderLayer.Game.Generated;
using CommanderLayer.Patches;
using CommanderLayer.Ui;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CommanderLayer.Composition
{
    /// <summary>
    /// Composition root: builds the commander service, hosts the modal (own overlay canvas, opened by the
    /// native CMD bezel button), draws the order overlay + live placement ring on the map's icon layer,
    /// routes clicks to order placement, runs the throttled management loop. Driven by the DynamicMap.Update
    /// Harmony postfix.
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
        private Text _cmdLabel;
        private Color _cmdLabelColor = Color.white;
        private OrderKind? _armed;
        private AssignmentPreview _hoverPreview;
        private bool _firstTick = true;
        private bool _loggedPanel;
        private float _nextManage;
        private GUIStyle _fallbackStyle;

        public CommanderRuntime()
        {
            _player = new GamePlayerContext();
            _projection = new DynamicMapProjection();
            _service = new CommanderService(new CommanderConfig { ArriveRadius = Plugin.ArriveRadius });
            Plugin.Log?.LogInfo("CommanderRuntime constructed.");
        }

        /// <summary>True while the modal is open (used by the map-pan guard).</summary>
        public bool ModalOpen => _screen != null && _screen.IsOpen;

        public void Tick()
        {
            if (_firstTick) { _firstTick = false; Plugin.Log?.LogInfo("CommanderRuntime first Tick — driver alive."); }

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

            if (!open)
            {
                if (_canvas != null) _canvas.enabled = false;
                _overlay?.Clear();         // hide markers/lines/ring when the map closes
                _armed = null;
                _hoverPreview = null;
                return;
            }
            if (_canvas != null) _canvas.enabled = true;

            // Live placement preview while armed.
            _hoverPreview = null;
            if (_armed.HasValue && !IsPointerOverUi() && _projection.TryCursorToWorld(out var hover))
            {
                bool isBuild = _armed.Value == OrderKind.Build;
                _hoverPreview = isBuild ? null : _service.PreviewAt(_armed.Value, hover, _screen.Domains, _screen.RangeMeters);
                bool canPlace = isBuild || (_hoverPreview != null && _hoverPreview.CanPlace);
                _overlay?.SetHover(hover, _screen.RangeMeters, _service.Config.ThreatRadius, _armed.Value, canPlace,
                    PreviewPositions(_hoverPreview));

                if (Input.GetMouseButtonDown(0))
                {
                    var state = _service.PlaceOrder(_armed.Value, hover, _screen.Domains, _screen.RangeMeters);
                    Plugin.Log?.LogInfo($"Placed {state.Order.Kind}: {state.Summary}");
                    _armed = null;
                    _overlay?.ClearHover();
                }
            }
            else
            {
                _overlay?.ClearHover();
            }

            if (Time.unscaledTime >= _nextManage)
            {
                _nextManage = Time.unscaledTime + _service.Config.ManagementIntervalSeconds;
                _service.Tick();
            }

            _player.TryGetLocalFaction(out var faction);
            _screen?.Render(_service.Orders, faction, _armed, _hoverPreview, NamesById());
            _screen?.RenderHq(_service.AutoHq(), _service.CurrentMode()); // mode selector + HQ readout
            _overlay?.Render(_service.Orders, PositionsById());

            if (_cmdLabel != null) _cmdLabel.color = ModalOpen ? new Color(0.4f, 1f, 0.5f) : _cmdLabelColor;

            if (ModalOpen && !_loggedPanel)
            {
                _loggedPanel = true;
                Plugin.Log?.LogInfo("[panel] " + _screen.DebugInfo());
            }
        }

        private Dictionary<string, Vec3> PositionsById()
        {
            var dict = new Dictionary<string, Vec3>();
            foreach (var u in _service.LastRoster) dict[u.Id] = u.Position;
            return dict;
        }

        private Dictionary<string, string> NamesById()
        {
            var dict = new Dictionary<string, string>();
            foreach (var u in _service.LastRoster) dict[u.Id] = u.Name;
            return dict;
        }

        // Positions of the units a hover preview would assign — drawn as lines so the player sees who responds.
        private static List<Vec3> PreviewPositions(AssignmentPreview preview)
        {
            if (preview == null) return null;
            var list = new List<Vec3>(preview.Assignable.Count);
            foreach (var u in preview.Assignable) list.Add(u.Position);
            return list;
        }

        public void AttachCmdButton(VirtualMFD mfd)
        {
            if (mfd == null || _cmdButton != null) return;
            EnsureCanvas();
            EnsureScreen();

            var rightButtons = GameSdk.VirtualMFD_rightButtons(mfd);
            var rightScreens = GameSdk.VirtualMFD_rightScreens(mfd);
            var leftButtons = GameSdk.VirtualMFD_leftButtons(mfd);
            var leftScreens = GameSdk.VirtualMFD_leftScreens(mfd);

            var btn = FindBlankButton(rightButtons, rightScreens) ?? FindBlankButton(leftButtons, leftScreens);
            if (btn == null) { Plugin.Log?.LogWarning("No blank MFD bezel button available for CMD."); return; }

            btn.enabled = true;
            btn.gameObject.SetActive(true);
            _cmdLabel = btn.GetComponentInChildren<Text>(includeInactive: true);
            if (_cmdLabel != null)
            {
                _cmdLabel.text = "CMD";
                _cmdLabel.enabled = true;
                _cmdLabel.gameObject.SetActive(true);
                _cmdLabelColor = _cmdLabel.color; // keep the game's native color; only override to green when open
            }
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => _screen?.Toggle());
            _cmdButton = btn;
            Plugin.Log?.LogInfo($"CMD button attached (label set={_cmdLabel != null}).");
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
            CaptureNativeButtonSprite();
            CaptureNativeAssets();
            _player.TryGetLocalFaction(out var faction);
            _theme = Theme.FromFaction(faction);
            _screen = new CommanderMapScreen(_canvas.transform, _theme,
                onArm: k => _armed = k,
                onClearAll: () => _service.ClearAll(),
                onClearOrder: id => _service.Clear(id),
                onSetMode: m => _service.SetMode(m),
                onConfirmProposal: () => _service.ConfirmTopProposal(),
                onToggleOpManual: id => _service.ToggleOperationManual(id));
            TryAddNativeBorder(_screen.PanelRoot, _theme.Accent);
            Plugin.Log?.LogInfo("Commander panel built.");
        }

        private static bool IsPointerOverUi()
            => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        // Frame the panel with the game's own procedural border via the native-UI component library
        // (NativeUi.Border -> NuclearOption.UI.BetterBorder) so it reads as a native window. The library call
        // is itself guarded — a styling failure must never break the working panel.
        private static void TryAddNativeBorder(RectTransform panel, Color accent)
            => CommanderLayer.Ui.Native.NativeUi.Border(panel, accent);

        // Capture the game's own visual resources (font, HUD colors, map/threat icons) from the single
        // codegen'd source of truth — NativeAssets, a typed snapshot of GameAssets — and mirror them into
        // the Ui-layer caches so our labels/cues/icons read as native. One read point; no scattered
        // GameAssets reads, no hardcoded/duplicated values. Drift in any asset fails the contract test.
        private static void CaptureNativeAssets()
        {
            var assets = CommanderLayer.Game.Generated.NativeAssets.Capture();
            if (assets == null) return;

            if (assets.playerNameFont != null)
            {
                UiFactory.Font = assets.playerNameFont;
                Plugin.Log?.LogInfo("Using native game font (GameAssets.playerNameFont).");
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
        private static void CaptureNativeButtonSprite()
        {
            if (UiFactory.ButtonSprite != null) return;
            foreach (var img in Resources.FindObjectsOfTypeAll<Image>())
            {
                if (img == null || img.sprite == null || img.type != Image.Type.Sliced) continue;
                if (!img.gameObject.scene.IsValid()) continue;      // skip prefabs/assets
                if (img.GetComponent<Button>() == null) continue;   // a real button's sprite
                UiFactory.ButtonSprite = img.sprite;
                return;
            }
        }

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
