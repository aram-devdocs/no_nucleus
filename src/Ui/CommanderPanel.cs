using System;
using System.Collections.Generic;
using System.Linq;
using CommanderLayer.Core.Model;
using CommanderLayer.Core.Planning;
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
        private readonly TextMeshProUGUI _hqHeader;
        private readonly TextMeshProUGUI _hqBody;
        private readonly TextMeshProUGUI _modeLabel;
        private readonly GameObject _confirmButton;
        private readonly TextMeshProUGUI _confirmLabel;
        private readonly Transform _ordersContainer;
        private readonly List<DomToggle> _domToggles = new List<DomToggle>();
        private readonly Image _attackImg, _defendImg, _captureImg, _resupplyImg, _buildImg, _moveImg;

        private struct DomToggle { public Image Img; public TextMeshProUGUI Label; public DomainSet Bit; public string Name; }
        private readonly Action<string> _onClearOrder;
        private readonly List<RowWidgets> _rows = new List<RowWidgets>();

        private DomainSet _domains = DomainSet.All;
        private int _rangeKm = 6;

        public RectTransform Root => _root;
        public DomainSet Domains => _domains;
        public float RangeMeters => _rangeKm * 1000f;

        private struct RowWidgets { public GameObject Go; public TextMeshProUGUI Label; public Button Clear; public string OrderId; }

        public CommanderPanel(Transform parent, Theme theme, Action<OrderKind> onArm, Action onClearAll,
            Action<string> onClearOrder, Action onCycleAutonomy = null, Action onConfirmProposal = null)
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

            // Domain toggles (checkbox-style: "[x] AIR" on, "[ ] AIR" off; single accent so state is obvious)
            UiFactory.Label("DomHint", layout.transform, "DOMAINS (who may be tasked)", 11f, theme.Muted);
            var domRow = UiFactory.HorizontalLayout("Domains", layout.transform, 6f);
            UiFactory.PreferredHeight(domRow.gameObject, 26f);
            AddToggle(domRow.transform, "AIR", DomainSet.Air);
            AddToggle(domRow.transform, "LAND", DomainSet.Land);
            AddToggle(domRow.transform, "SEA", DomainSet.Sea);

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

            // Autonomous-commander HQ readout + controls (the commander runs by default).
            _hqHeader = UiFactory.Label("HqHeader", layout.transform, "", 14f, theme.Accent);
            UiFactory.PreferredHeight(_hqHeader.gameObject, 22f);

            // MODE: cycle Auto -> Assisted -> Manual; Confirm: authorise the top Assisted suggestion.
            var hqControls = UiFactory.HorizontalLayout("HqControls", layout.transform, 6f);
            UiFactory.PreferredHeight(hqControls.gameObject, 26f);
            var modeBtn = UiFactory.Button("Mode", hqControls.transform, "MODE: AUTO", theme, () => onCycleAutonomy?.Invoke());
            _modeLabel = modeBtn.GetComponentInChildren<TextMeshProUGUI>();
            var confirmBtn = UiFactory.Button("Confirm", hqControls.transform, "Confirm", theme, () => onConfirmProposal?.Invoke());
            _confirmButton = confirmBtn.gameObject;
            _confirmLabel = confirmBtn.GetComponentInChildren<TextMeshProUGUI>();

            _hqBody = UiFactory.Label("HqBody", layout.transform, "", 12f, theme.Muted);
            UiFactory.PreferredHeight(_hqBody.gameObject, 120f);

            RefreshControls();
        }

        /// <summary>Render the autonomous commander's HQ: active operations + the recent battle feed.</summary>
        public void RenderHq(CommanderLayer.Core.Command.HqSnapshot hq)
        {
            if (_hqHeader == null) return;
            string mode = hq != null ? hq.CommanderAutonomy.ToString().ToUpperInvariant() : "AUTO";
            if (_modeLabel != null) _modeLabel.text = "MODE: " + mode;             // reflect + cycle autonomy
            int proposalCount = hq?.Proposals.Count ?? 0;
            if (_confirmButton != null) _confirmButton.SetActive(proposalCount > 0); // only when a suggestion waits
            if (_confirmLabel != null) _confirmLabel.text = proposalCount > 0 ? $"Confirm ({proposalCount})" : "Confirm";

            if (hq == null || (hq.Operations.Count == 0 && hq.Recent.Count == 0 && hq.Squads.Count == 0
                && hq.Proposals.Count == 0))
            {
                _hqHeader.text = "";
                _hqBody.text = "";
                return;
            }
            _hqHeader.text = $"{mode} COMMANDER · {hq.Operations.Count} op(s) · {hq.Squads.Count} squad(s)";
            var sb = new System.Text.StringBuilder();
            foreach (var op in hq.Operations.Take(3))
                sb.AppendLine($"• {op.Kind} — {op.Phase} [{op.Status}]");
            foreach (var sq in hq.Squads.Take(3))
                sb.AppendLine($"▣ {sq.Name} · {sq.Family} ×{sq.Strength} [{sq.Status}]");
            // Assisted suggestions awaiting the player's confirm.
            foreach (var p in hq.Proposals.Take(3)) sb.AppendLine($"? {p.Summary} — confirm to launch");
            foreach (var line in hq.Production.Take(2)) sb.AppendLine(line);
            foreach (var e in hq.Recent.Take(4)) sb.AppendLine($"· {e.Text}");
            _hqBody.text = sb.ToString().TrimEnd();
        }

        public void SetVisible(bool visible)
        {
            if (_root != null) _root.gameObject.SetActive(visible);
        }

        public void Render(IReadOnlyList<OrderState> orders, FactionInfo faction, OrderKind? armed, AssignmentPreview preview,
            IReadOnlyDictionary<string, string> unitNames = null)
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
                if (n > 0)
                {
                    string names = string.Join(", ", preview.Assignable.Take(4).Select(u => u.Name));
                    if (n > 4) names += $" +{n - 4}";
                    _status.text = $"{armed.Value} → {n} will respond: {names}";
                }
                else
                {
                    _status.text = $"{armed.Value}: no units available — widen range/domains (or all are tasked)";
                }
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
                    r.Label.text = $"{o.Order.Kind.ToString().ToUpperInvariant()} · {BattlePlan.Label(o.Phase)}{UnitSuffix(o, unitNames)}";
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

        // The order row's assigned-unit list: up to a few names (or ids if no name map), with overflow count.
        private static string UnitSuffix(OrderState o, IReadOnlyDictionary<string, string> names)
        {
            int n = o.AssignedUnitIds.Count;
            if (n == 0) return "";
            if (names == null) return $" · {n} unit{(n == 1 ? "" : "s")}";
            const int show = 4;
            string list = string.Join(", ", o.AssignedUnitIds.Take(show)
                .Select(id => names.TryGetValue(id, out var nm) ? nm : id));
            if (n > show) list += $" +{n - show}";
            return " · " + list;
        }

        private void AddToggle(Transform parent, string name, DomainSet bit)
        {
            var btn = UiFactory.Button("Dom_" + name, parent, name, _theme, () => Flip(bit));
            var lbl = btn.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
            _domToggles.Add(new DomToggle { Img = btn.GetComponent<Image>(), Label = lbl, Bit = bit, Name = name });
        }

        private void Flip(DomainSet bit) { _domains ^= bit; RefreshControls(); }
        private void StepRange(int delta) { _rangeKm = Mathf.Clamp(_rangeKm + delta, 1, 20); RefreshControls(); }

        private void RefreshControls()
        {
            foreach (var t in _domToggles)
            {
                bool on = (_domains & t.Bit) != 0;
                t.Img.color = on ? _theme.Accent : _theme.ButtonIdle;             // single accent = ON, dim = OFF
                if (t.Label != null) t.Label.text = (on ? "[x] " : "[ ] ") + t.Name; // unambiguous checkbox glyph
            }
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
