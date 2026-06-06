using System.Collections.Generic;
using Nucleus.Core.Model;
using Nucleus.Core.Ports;
using Nucleus.Game;
using Nucleus.Ui;
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
        private OrderKind? _armed;
        private AssignmentPreview _hoverPreview;
        private bool _firstTick = true;
        private bool _loggedPanel;
        private float _nextManage;

        public CommanderRuntime()
        {
            _player = new GamePlayerContext();
            _projection = new DynamicMapProjection();
            _service = new CommanderService(new CommanderConfig { ArriveRadius = CommanderPlugin.ArriveRadius });
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
            // CMD screen = manual orders + commander mode only — built the SAME way as every other mod
            // (a CommanderPanel filling the host's standard ModPanel chrome). Build/Squad/Warfare own the rest.
            _panel = new CommanderPanel(parent, _theme,
                onArm: k => _armed = k,
                onClearAll: () => _service.ClearAll(),
                onClearOrder: id => _service.Clear(id),
                onSetMode: m => _service.SetMode(m),
                onConfirmProposal: () => _service.ConfirmTopProposal(),
                onToggleOpManual: id => _service.ToggleOperationManual(id),
                onToggleSquadManual: id => _service.ToggleSquadManual(id),
                onBuyConvoy: name => _service.BuyConvoy(name),
                sections: CommanderPanel.PanelSections.Orders | CommanderPanel.PanelSections.Mode);
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

            _hoverPreview = null;
            if (!open)
            {
                _overlay?.Clear();
                _armed = null;
            }
            else if (_panel != null)
            {
                // Live placement preview while armed.
                if (_armed.HasValue && !IsPointerOverUi() && _projection.TryCursorToWorld(out var hover))
                {
                    bool isBuild = _armed.Value == OrderKind.Build;
                    _hoverPreview = isBuild ? null : _service.PreviewAt(_armed.Value, hover, _panel.Domains, _panel.RangeMeters);
                    bool canPlace = isBuild || (_hoverPreview != null && _hoverPreview.CanPlace);
                    _overlay?.SetHover(hover, _panel.RangeMeters, _service.Config.ThreatRadius, _armed.Value, canPlace,
                        PreviewPositions(_hoverPreview));

                    if (Input.GetMouseButtonDown(0))
                    {
                        var state = _service.PlaceOrder(_armed.Value, hover, _panel.Domains, _panel.RangeMeters);
                        CommanderPlugin.Log?.LogInfo($"Placed {state.Order.Kind}: {state.Summary}");
                        _armed = null;
                        _overlay?.ClearHover();
                    }
                }
                else
                {
                    _overlay?.ClearHover();
                }
                _overlay?.Render(_service.Orders, PositionsById());
            }

            // Keep the panel content fresh (it renders only when the native screen shows it).
            if (_panel != null)
            {
                _player.TryGetLocalFaction(out var faction);
                _panel.Render(_service.Orders, faction, _armed, _hoverPreview, NamesById());
                _panel.RenderHq(_service.AutoHq(), _service.CurrentMode(), _service.BuildCatalog(), _service.Funds());
                if (!_loggedPanel) { _loggedPanel = true; CommanderPlugin.Log?.LogInfo("[panel] Commander panel rendering."); }
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
    }
}
