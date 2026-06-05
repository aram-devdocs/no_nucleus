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

        public CommanderMapScreen(Transform parent, Theme theme, Action<OrderKind> onArm, Action onClearAll, Action<string> onClearOrder)
        {
            _container = UiFactory.Panel("CommanderScreen", parent, new Color(0f, 0f, 0f, 0f));
            UiFactory.AnchorTopLeft(_container, new Vector2(360f, 520f), new Vector2(90f, 90f));

            var header = UiFactory.Panel("DragBar", _container, theme.TabBackground);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, 22f);
            header.anchoredPosition = Vector2.zero;
            var grab = UiFactory.Label("DragHint", header, "⠿  COMMANDER  —  drag", 12f, theme.Muted, TMPro.TextAlignmentOptions.Center);
            UiFactory.Stretch(grab.rectTransform);
            header.gameObject.AddComponent<DragHandle>().Target = _container;

            _panel = new CommanderPanel(_container, theme, onArm, onClearAll, onClearOrder);
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

        public void Toggle() => SetOpen(!_open);

        public void SetOpen(bool open)
        {
            _open = open;
            if (_container != null) _container.gameObject.SetActive(open);
        }

        public void Render(IReadOnlyList<OrderState> orders, FactionInfo faction, OrderKind? armed, AssignmentPreview preview)
            => _panel.Render(orders, faction, armed, preview);
    }
}
