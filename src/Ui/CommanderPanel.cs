using System;
using System.Collections.Generic;
using CommanderLayer.Core.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CommanderLayer.Ui
{
    /// <summary>
    /// The Commander modal content (prop-driven). Lets the player arm an order kind (then click the map),
    /// clear all, and see the live order list with assignment summaries. No game access.
    /// </summary>
    public sealed class CommanderPanel
    {
        private readonly Theme _theme;
        private readonly RectTransform _root;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _status;
        private readonly TextMeshProUGUI _ordersHeader;
        private readonly Transform _ordersContainer;
        private readonly Image _attackImg;
        private readonly Image _defendImg;
        private readonly List<TextMeshProUGUI> _rows = new List<TextMeshProUGUI>();

        public RectTransform Root => _root;

        public CommanderPanel(Transform parent, Theme theme, Action<OrderKind> onArm, Action onClearAll)
        {
            _theme = theme;
            _root = UiFactory.Panel("CommanderPanel", parent, theme.PanelBackground);
            var layout = UiFactory.VerticalLayout("Layout", _root, 6f, new RectOffset(10, 10, 10, 10));
            UiFactory.Stretch((RectTransform)layout.transform);

            _title = UiFactory.Label("Title", layout.transform, "COMMANDER", 18f, theme.Accent);
            UiFactory.PreferredHeight(_title.gameObject, 24f);
            _status = UiFactory.Label("Status", layout.transform, "", 13f, theme.Muted);
            UiFactory.PreferredHeight(_status.gameObject, 34f);

            var row = UiFactory.VerticalLayout("Buttons", layout.transform, 6f, new RectOffset(0, 0, 0, 0));
            var attack = UiFactory.Button("AttackBtn", row.transform, "Attack  (click map)", theme, () => onArm?.Invoke(OrderKind.Attack));
            UiFactory.PreferredHeight(attack.gameObject, 30f);
            _attackImg = attack.GetComponent<Image>();
            var defend = UiFactory.Button("DefendBtn", row.transform, "Defend  (click map)", theme, () => onArm?.Invoke(OrderKind.Defend));
            UiFactory.PreferredHeight(defend.gameObject, 30f);
            _defendImg = defend.GetComponent<Image>();
            var clear = UiFactory.Button("ClearAll", row.transform, "Clear all orders", theme, () => onClearAll?.Invoke());
            UiFactory.PreferredHeight(clear.gameObject, 26f);

            _ordersHeader = UiFactory.Label("OrdersHeader", layout.transform, "Orders", 14f, theme.Text);
            UiFactory.PreferredHeight(_ordersHeader.gameObject, 22f);
            _ordersContainer = UiFactory.VerticalLayout("Orders", layout.transform, 3f, new RectOffset(0, 0, 0, 0)).transform;
        }

        public void SetVisible(bool visible)
        {
            if (_root != null) _root.gameObject.SetActive(visible);
        }

        public void Render(IReadOnlyList<OrderState> orders, FactionInfo faction, OrderKind? armed)
        {
            if (_root == null) return;

            _title.text = faction != null ? $"COMMANDER — {faction.Name}" : "COMMANDER";
            _status.text = armed.HasValue
                ? $"Click the map to place a {armed.Value} order."
                : "Pick Attack or Defend, then click the map.";

            // Highlight the armed button.
            _attackImg.color = armed == OrderKind.Attack ? OrderColors.Attack : _theme.Accent;
            _defendImg.color = armed == OrderKind.Defend ? OrderColors.Defend : _theme.Accent;

            _ordersHeader.text = $"Orders: {orders.Count}";
            EnsureRows(orders.Count);
            for (int i = 0; i < _rows.Count; i++)
            {
                if (i < orders.Count)
                {
                    var o = orders[i];
                    _rows[i].text = $"{o.Order.Kind.ToString().ToUpperInvariant()} · {o.Summary}";
                    _rows[i].color = o.Status == OrderStatus.Failed ? new Color(1f, 0.5f, 0.5f)
                        : o.Status == OrderStatus.Complete ? _theme.Arrived
                        : OrderColors.For(o.Order.Kind);
                    _rows[i].gameObject.SetActive(true);
                }
                else
                {
                    _rows[i].gameObject.SetActive(false);
                }
            }
        }

        private void EnsureRows(int count)
        {
            while (_rows.Count < count)
            {
                var r = UiFactory.Label("Order" + _rows.Count, _ordersContainer, "", 12f, _theme.Text);
                UiFactory.PreferredHeight(r.gameObject, 16f);
                _rows.Add(r);
            }
        }
    }
}
