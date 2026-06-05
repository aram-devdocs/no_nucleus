using System;
using System.Collections.Generic;
using CommanderLayer.Core.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CommanderLayer.Ui
{
    /// <summary>
    /// Commander modal content. Player picks domains (air/land/sea), a pull radius, then arms Attack/Defend
    /// and clicks the map. Shows the live order list with per-order clear. Owns its control state
    /// (Domains/RangeMeters); the runtime reads those when previewing/placing. No game access.
    /// </summary>
    public sealed class CommanderPanel
    {
        private readonly Theme _theme;
        private readonly RectTransform _root;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _status;
        private readonly TextMeshProUGUI _rangeLabel;
        private readonly TextMeshProUGUI _ordersHeader;
        private readonly Transform _ordersContainer;
        private readonly Image _airImg, _landImg, _seaImg;
        private readonly Image _attackImg, _defendImg, _captureImg, _resupplyImg, _buildImg, _moveImg;
        private readonly Action<string> _onClearOrder;
        private readonly List<RowWidgets> _rows = new List<RowWidgets>();

        private DomainSet _domains = DomainSet.All;
        private int _rangeKm = 6;

        public RectTransform Root => _root;
        public DomainSet Domains => _domains;
        public float RangeMeters => _rangeKm * 1000f;

        private struct RowWidgets { public GameObject Go; public TextMeshProUGUI Label; public Button Clear; public string OrderId; }

        public CommanderPanel(Transform parent, Theme theme, Action<OrderKind> onArm, Action onClearAll, Action<string> onClearOrder)
        {
            _theme = theme;
            _onClearOrder = onClearOrder;
            _root = UiFactory.Panel("CommanderPanel", parent, theme.PanelBackground);
            var layout = UiFactory.VerticalLayout("Layout", _root, 6f, new RectOffset(10, 10, 10, 10));
            UiFactory.Stretch((RectTransform)layout.transform);

            _title = UiFactory.Label("Title", layout.transform, "COMMANDER", 18f, theme.Accent);
            UiFactory.PreferredHeight(_title.gameObject, 24f);
            _status = UiFactory.Label("Status", layout.transform, "", 13f, theme.Muted);
            UiFactory.PreferredHeight(_status.gameObject, 32f);

            // Domain toggles
            var domRow = UiFactory.HorizontalLayout("Domains", layout.transform, 6f);
            UiFactory.PreferredHeight(domRow.gameObject, 26f);
            _airImg = ToggleButton(domRow.transform, "AIR", () => Flip(DomainSet.Air));
            _landImg = ToggleButton(domRow.transform, "LAND", () => Flip(DomainSet.Land));
            _seaImg = ToggleButton(domRow.transform, "SEA", () => Flip(DomainSet.Sea));

            // Range stepper
            var rangeRow = UiFactory.HorizontalLayout("Range", layout.transform, 6f);
            UiFactory.PreferredHeight(rangeRow.gameObject, 26f);
            UiFactory.Button("RangeDown", rangeRow.transform, "Range −", theme, () => StepRange(-1));
            _rangeLabel = UiFactory.Label("RangeLabel", rangeRow.transform, "", 13f, theme.Text, TextAlignmentOptions.Center);
            UiFactory.Button("RangeUp", rangeRow.transform, "Range +", theme, () => StepRange(+1));

            // Arm + clear
            var armRow = UiFactory.HorizontalLayout("Arm", layout.transform, 6f);
            UiFactory.PreferredHeight(armRow.gameObject, 30f);
            _attackImg = UiFactory.Button("Attack", armRow.transform, "Attack", theme, () => onArm?.Invoke(OrderKind.Attack)).GetComponent<Image>();
            _defendImg = UiFactory.Button("Defend", armRow.transform, "Defend", theme, () => onArm?.Invoke(OrderKind.Defend)).GetComponent<Image>();

            var armRow2 = UiFactory.HorizontalLayout("Arm2", layout.transform, 6f);
            UiFactory.PreferredHeight(armRow2.gameObject, 30f);
            _captureImg = UiFactory.Button("Capture", armRow2.transform, "Capture", theme, () => onArm?.Invoke(OrderKind.Capture)).GetComponent<Image>();
            _resupplyImg = UiFactory.Button("Resupply", armRow2.transform, "Resupply", theme, () => onArm?.Invoke(OrderKind.Resupply)).GetComponent<Image>();
            _buildImg = UiFactory.Button("Build", armRow2.transform, "Build", theme, () => onArm?.Invoke(OrderKind.Build)).GetComponent<Image>();

            var armRow3 = UiFactory.HorizontalLayout("Arm3", layout.transform, 6f);
            UiFactory.PreferredHeight(armRow3.gameObject, 30f);
            _moveImg = UiFactory.Button("Move", armRow3.transform, "Move", theme, () => onArm?.Invoke(OrderKind.Move)).GetComponent<Image>();

            var clearAll = UiFactory.Button("ClearAll", layout.transform, "Clear all orders", theme, () => onClearAll?.Invoke());
            UiFactory.PreferredHeight(clearAll.gameObject, 24f);

            _ordersHeader = UiFactory.Label("OrdersHeader", layout.transform, "Orders", 14f, theme.Text);
            UiFactory.PreferredHeight(_ordersHeader.gameObject, 22f);
            _ordersContainer = UiFactory.VerticalLayout("Orders", layout.transform, 3f, new RectOffset(0, 0, 0, 0)).transform;

            RefreshControls();
        }

        public void SetVisible(bool visible)
        {
            if (_root != null) _root.gameObject.SetActive(visible);
        }

        public void Render(IReadOnlyList<OrderState> orders, FactionInfo faction, OrderKind? armed, AssignmentPreview preview)
        {
            if (_root == null) return;

            _title.text = faction != null ? $"COMMANDER — {faction.Name}" : "COMMANDER";
            if (armed == OrderKind.Build)
            {
                _status.text = "Build: click the map to commission a convoy (queued at your base).";
            }
            else if (armed.HasValue)
            {
                int n = preview != null ? preview.Count : 0;
                string can = n > 0 ? $"{n} unit(s) will respond" : "no units in range — widen range or domains";
                _status.text = $"{armed.Value}: click the map  ·  {can}";
            }
            else
            {
                _status.text = "Pick domains + range, then Attack/Defend, then click the map.";
            }
            _attackImg.color = armed == OrderKind.Attack ? OrderColors.Attack : _theme.ButtonIdle;
            _defendImg.color = armed == OrderKind.Defend ? OrderColors.Defend : _theme.ButtonIdle;
            _captureImg.color = armed == OrderKind.Capture ? OrderColors.Capture : _theme.ButtonIdle;
            _resupplyImg.color = armed == OrderKind.Resupply ? OrderColors.Resupply : _theme.ButtonIdle;
            _buildImg.color = armed == OrderKind.Build ? OrderColors.Build : _theme.ButtonIdle;
            _moveImg.color = armed == OrderKind.Move ? OrderColors.Move : _theme.ButtonIdle;

            _ordersHeader.text = $"Orders: {orders.Count}";
            EnsureRows(orders.Count);
            for (int i = 0; i < _rows.Count; i++)
            {
                if (i < orders.Count)
                {
                    var o = orders[i];
                    var r = _rows[i];
                    r.OrderId = o.Order.Id;
                    int n = o.AssignedUnitIds.Count;
                    string units = n > 0 ? $" · {n} unit{(n == 1 ? "" : "s")}" : "";
                    r.Label.text = $"{o.Order.Kind.ToString().ToUpperInvariant()} · {CommanderLayer.Core.Planning.BattlePlan.Label(o.Phase)}{units}";
                    r.Label.color = o.Status == OrderStatus.Failed ? new Color(1f, 0.5f, 0.5f)
                        : o.Status == OrderStatus.Complete ? _theme.Arrived
                        : OrderColors.For(o.Order.Kind);
                    r.Go.SetActive(true);
                    _rows[i] = r;
                }
                else
                {
                    _rows[i].Go.SetActive(false);
                }
            }
        }

        private Image ToggleButton(Transform parent, string text, Action onClick)
            => UiFactory.Button("Dom_" + text, parent, text, _theme, () => onClick()).GetComponent<Image>();

        private void Flip(DomainSet bit) { _domains ^= bit; RefreshControls(); }
        private void StepRange(int delta) { _rangeKm = Mathf.Clamp(_rangeKm + delta, 1, 20); RefreshControls(); }

        private void RefreshControls()
        {
            _airImg.color = (_domains & DomainSet.Air) != 0 ? OrderColors.Defend : _theme.ButtonIdle;
            _landImg.color = (_domains & DomainSet.Land) != 0 ? OrderColors.Resupply : _theme.ButtonIdle;
            _seaImg.color = (_domains & DomainSet.Sea) != 0 ? OrderColors.Attack : _theme.ButtonIdle;
            if (_rangeLabel != null) _rangeLabel.text = $"{_rangeKm}.0 km";
        }

        private void EnsureRows(int count)
        {
            while (_rows.Count < count)
            {
                var row = UiFactory.HorizontalLayout("OrderRow" + _rows.Count, _ordersContainer, 4f);
                UiFactory.PreferredHeight(row.gameObject, 18f);
                var label = UiFactory.Label("L", row.transform, "", 12f, _theme.Text);
                var clearBtn = UiFactory.Button("X", row.transform, "✕", _theme, null);
                var le = clearBtn.gameObject.GetComponent<LayoutElement>() ?? clearBtn.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 22f; le.flexibleWidth = 0f;
                int idx = _rows.Count;
                clearBtn.onClick.AddListener(() => { var id = _rows[idx].OrderId; if (id != null) _onClearOrder?.Invoke(id); });
                _rows.Add(new RowWidgets { Go = row.gameObject, Label = label, Clear = clearBtn });
            }
        }
    }
}
