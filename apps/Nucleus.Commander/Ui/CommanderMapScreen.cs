using System;
using System.Collections.Generic;
using CommanderLayer.Core.Model;
using UnityEngine;

namespace CommanderLayer.Ui
{
    /// <summary>
    /// The Commander modal on our overlay canvas, opened/closed by the native CMD map button. Starts
    /// hidden. Hosts a drag bar (move) and the panel. Exposes the panel's domain/range selections + open
    /// state to the runtime. The marker overlay is owned by the runtime.
    /// </summary>
    public sealed class CommanderMapScreen
    {
        private readonly CommanderPanel _panel;
        private readonly RectTransform _container;
        private bool _open;

        public CommanderMapScreen(Transform parent, Theme theme, Action<OrderKind> onArm, Action onClearAll,
            Action<string> onClearOrder, Action<CommanderLayer.Core.Command.CommanderMode> onSetMode = null,
            Action onConfirmProposal = null, Action<string> onToggleOpManual = null,
            Action<string> onToggleSquadManual = null, Action<string> onBuyConvoy = null)
        {
            // Left-docked, fixed size, tall enough that all sections fit without compressing (the jerk).
            _container = UiFactory.Panel("CommanderScreen", parent, new Color(0f, 0f, 0f, 0f));
            UiFactory.AnchorTopLeft(_container, new Vector2(430f, 840f), new Vector2(24f, 70f));

            var header = UiFactory.Panel("DragBar", _container, theme.TabBackground);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, 22f);
            header.anchoredPosition = Vector2.zero;
            var grab = UiFactory.Label("DragHint", header, "COMMANDER  (drag to move)", 12f, theme.Muted, TMPro.TextAlignmentOptions.Center);
            UiFactory.Stretch(grab.rectTransform);
            header.gameObject.AddComponent<DragHandle>().Target = _container;

            _panel = new CommanderPanel(_container, theme, onArm, onClearAll, onClearOrder, onSetMode,
                onConfirmProposal, onToggleOpManual, onToggleSquadManual, onBuyConvoy);
            var p = _panel.Root;
            p.anchorMin = Vector2.zero;
            p.anchorMax = Vector2.one;
            p.offsetMin = Vector2.zero;
            p.offsetMax = new Vector2(0f, -24f);

            SetOpen(false);
        }

        public bool IsOpen => _open;
        public DomainSet Domains => _panel.Domains;
        public float RangeMeters => _panel.RangeMeters;
        public RectTransform PanelRoot => _panel.Root;

        public void Toggle() => SetOpen(!_open);

        public void SetOpen(bool open)
        {
            _open = open;
            if (_container != null) _container.gameObject.SetActive(open);
        }

        public void Render(IReadOnlyList<OrderState> orders, FactionInfo faction, OrderKind? armed, AssignmentPreview preview,
            IReadOnlyDictionary<string, string> unitNames = null)
            => _panel.Render(orders, faction, armed, preview, unitNames);

        public void RenderHq(CommanderLayer.Core.Command.HqSnapshot hq, CommanderLayer.Core.Command.CommanderMode mode,
            CommanderLayer.Core.Command.ConvoyCatalog catalog, float funds)
            => _panel.RenderHq(hq, mode, catalog, funds);

        public string DebugInfo()
        {
            var p = _panel.Root;
            return $"container active={_container.gameObject.activeInHierarchy} size={_container.rect.size} | panel size={(p != null ? p.rect.size.ToString() : "null")}";
        }
    }
}
