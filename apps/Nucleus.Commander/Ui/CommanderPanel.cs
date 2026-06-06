using System;
using System.Collections.Generic;
using System.Linq;
using CommanderLayer.Core.Model;
using CommanderLayer.Core.Planning;
using Cmd = CommanderLayer.Core.Command;
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
        private readonly TextMeshProUGUI _modeDesc;
        private readonly List<ModeBtn> _modeBtns = new List<ModeBtn>();
        private readonly GameObject _confirmButton;
        private readonly TextMeshProUGUI _confirmLabel;
        private readonly Transform _opsContainer;
        private readonly Action<string> _onToggleOpManual;
        private readonly Action<string> _onToggleSquadManual;
        private readonly Action<string> _onBuyConvoy;
        private readonly List<OpRow> _opRows = new List<OpRow>();
        private readonly Transform _squadsContainer;
        private readonly List<EntityRow> _squadRows = new List<EntityRow>();
        private readonly Transform _buildContainer;
        private readonly List<EntityRow> _buildRows = new List<EntityRow>();

        private struct OpRow { public GameObject Go; public TextMeshProUGUI Label; public Image BtnImg; public TextMeshProUGUI BtnLabel; public string OpId; }
        // Generic interactive row: a label + an action button carrying an id (squad id / convoy name).
        private struct EntityRow { public GameObject Go; public TextMeshProUGUI Label; public Image BtnImg; public TextMeshProUGUI BtnLabel; public string Id; }
        private struct ModeBtn { public Image Img; public CommanderLayer.Core.Command.CommanderMode Mode; }

        // One-line description shown under the selector for the active mode (the "definitive selections").
        private static string ModeDescription(CommanderLayer.Core.Command.CommanderMode m)
        {
            switch (m)
            {
                case CommanderLayer.Core.Command.CommanderMode.Off: return "OFF — the game's own AI runs the war. The Commander does nothing.";
                case CommanderLayer.Core.Command.CommanderMode.Manual: return "MANUAL — you command by hand. The Commander shows your squads + targets but issues no orders.";
                case CommanderLayer.Core.Command.CommanderMode.Assisted: return "ASSISTED — the Commander proposes operations; nothing runs until you press Confirm.";
                default: return "AUTO — the Commander runs the whole war. Override any operation with its AUTO/MANUAL toggle.";
            }
        }
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
            Action<string> onClearOrder, Action<CommanderLayer.Core.Command.CommanderMode> onSetMode = null,
            Action onConfirmProposal = null, Action<string> onToggleOpManual = null,
            Action<string> onToggleSquadManual = null, Action<string> onBuyConvoy = null)
        {
            _theme = theme;
            _onClearOrder = onClearOrder;
            _onToggleOpManual = onToggleOpManual;
            _onToggleSquadManual = onToggleSquadManual;
            _onBuyConvoy = onBuyConvoy;
            _root = UiFactory.Panel("CommanderPanel", parent, theme.PanelBackground);
            // Scrollable content: a clipped viewport + a content column whose height fits its children, so the
            // many sections never compress (the jerk) — they extend and the panel scrolls instead.
            var viewport = UiFactory.Panel("Viewport", _root, new Color(0f, 0f, 0f, 0f));
            UiFactory.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var layout = UiFactory.VerticalLayout("Layout", viewport, 6f, new RectOffset(10, 10, 10, 10));
            var lrt = (RectTransform)layout.transform;
            lrt.anchorMin = new Vector2(0f, 1f); lrt.anchorMax = new Vector2(1f, 1f);
            lrt.pivot = new Vector2(0.5f, 1f); lrt.anchoredPosition = Vector2.zero; lrt.sizeDelta = Vector2.zero;
            var fitter = layout.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = _root.gameObject.AddComponent<ScrollRect>();
            scroll.content = lrt; scroll.viewport = viewport;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 24f;

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
            UiFactory.Button("RangeDown", rangeRow.transform, "Range -", theme, () => StepRange(-1));
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

            // COMMANDER MODE — the single control (no F1 needed): OFF / MANUAL / ASSISTED / AUTO.
            _hqHeader = UiFactory.Label("HqHeader", layout.transform, "COMMANDER", 14f, theme.Accent);
            UiFactory.PreferredHeight(_hqHeader.gameObject, 22f);
            var modeRow = UiFactory.HorizontalLayout("ModeRow", layout.transform, 4f);
            UiFactory.PreferredHeight(modeRow.gameObject, 28f);
            AddModeButton(modeRow.transform, "OFF", Cmd.CommanderMode.Off, onSetMode);
            AddModeButton(modeRow.transform, "MANUAL", Cmd.CommanderMode.Manual, onSetMode);
            AddModeButton(modeRow.transform, "ASSIST", Cmd.CommanderMode.Assisted, onSetMode);
            AddModeButton(modeRow.transform, "AUTO", Cmd.CommanderMode.Auto, onSetMode);
            _modeDesc = UiFactory.Label("ModeDesc", layout.transform, "", 11f, theme.Muted);
            UiFactory.PreferredHeight(_modeDesc.gameObject, 30f);
            var confirmBtn = UiFactory.Button("Confirm", layout.transform, "Confirm proposal", theme, () => onConfirmProposal?.Invoke());
            UiFactory.PreferredHeight(confirmBtn.gameObject, 24f);
            _confirmButton = confirmBtn.gameObject;
            _confirmLabel = confirmBtn.GetComponentInChildren<TextMeshProUGUI>();

            // OPERATIONS — one interactive row per op with an AUTO/MANUAL toggle (take a slice).
            UiFactory.PreferredHeight(UiFactory.Label("OpsHdr", layout.transform, "OPERATIONS", 12f, theme.Accent).gameObject, 18f);
            _opsContainer = UiFactory.VerticalLayout("HqOps", layout.transform, 3f, new RectOffset(0, 0, 0, 0)).transform;

            // SQUADS — name + what it's doing + an AUTO/MANUAL toggle (manage each squad).
            UiFactory.PreferredHeight(UiFactory.Label("SquadsHdr", layout.transform, "SQUADS", 12f, theme.Accent).gameObject, 18f);
            _squadsContainer = UiFactory.VerticalLayout("HqSquads", layout.transform, 3f, new RectOffset(0, 0, 0, 0)).transform;

            // BUILD — buy troops: a row per convoy (name + contents + cost) with a BUY button.
            UiFactory.PreferredHeight(UiFactory.Label("BuildHdr", layout.transform, "BUILD — buy troops", 12f, theme.Accent).gameObject, 18f);
            _buildContainer = UiFactory.VerticalLayout("HqBuild", layout.transform, 3f, new RectOffset(0, 0, 0, 0)).transform;

            // FEED — production status + recent battle events (what the commander is doing).
            UiFactory.PreferredHeight(UiFactory.Label("FeedHdr", layout.transform, "FEED", 12f, theme.Accent).gameObject, 18f);
            _hqBody = UiFactory.Label("HqBody", layout.transform, "", 12f, theme.Muted);
            UiFactory.PreferredHeight(_hqBody.gameObject, 110f);

            RefreshControls();
        }

        private void AddModeButton(Transform parent, string label, Cmd.CommanderMode mode,
            Action<Cmd.CommanderMode> onSetMode)
        {
            var btn = UiFactory.Button("Mode_" + label, parent, label, _theme, () => onSetMode?.Invoke(mode));
            _modeBtns.Add(new ModeBtn { Img = btn.GetComponent<Image>(), Mode = mode });
        }

        /// <summary>Render the commander mode selector (always) + the HQ readout (when the commander is on).</summary>
        public void RenderHq(Cmd.HqSnapshot hq, Cmd.CommanderMode mode, Cmd.ConvoyCatalog catalog, float funds)
        {
            if (_modeDesc == null) return;
            // Mode selector — always shown so OFF can be switched on; active mode highlighted + described.
            foreach (var mb in _modeBtns) mb.Img.color = mb.Mode == mode ? _theme.Accent : _theme.ButtonIdle;
            _modeDesc.text = ModeDescription(mode);

            int proposalCount = hq?.Proposals.Count ?? 0;
            bool showConfirm = mode == Cmd.CommanderMode.Assisted && proposalCount > 0;
            if (_confirmButton != null) _confirmButton.SetActive(showConfirm);
            if (_confirmLabel != null) _confirmLabel.text = $"Confirm next proposal ({proposalCount})";

            // BUILD menu is available whenever a faction/catalog exists (buy in any mode, even OFF).
            RenderBuildRows(catalog, funds);

            // Operations/squads/feed only when the commander is actually running.
            if (hq == null || mode == Cmd.CommanderMode.Off)
            {
                _hqBody.text = "";
                RenderOpRows(null);
                RenderSquadRows(null);
                return;
            }
            RenderOpRows(hq.Operations);     // interactive op rows (AUTO/MANUAL per op)
            RenderSquadRows(hq.Squads);      // interactive squad rows (AUTO/MANUAL + activity)
            var sb = new System.Text.StringBuilder();
            foreach (var p in hq.Proposals.Take(3)) sb.AppendLine($"? {p.Summary} — press Confirm");
            foreach (var line in hq.Production.Take(3)) sb.AppendLine(line);
            foreach (var e in hq.Recent.Take(5)) sb.AppendLine($"· {e.Text}");
            _hqBody.text = sb.ToString().TrimEnd();
        }

        // Squad rows: "Name · Family ×N — activity" + an AUTO/MANUAL toggle. Pooled + index-captured.
        private void RenderSquadRows(System.Collections.Generic.IReadOnlyList<Cmd.SquadView> squads)
        {
            int count = squads?.Count ?? 0;
            EnsureEntityRows(_squadRows, _squadsContainer, System.Math.Min(count, 6), "Squad",
                id => _onToggleSquadManual?.Invoke(id));
            for (int i = 0; i < _squadRows.Count; i++)
            {
                var r = _squadRows[i];
                if (squads != null && i < count && i < 6)
                {
                    var s = squads[i];
                    r.Id = s.Id;
                    r.Label.text = $"{s.Name} · {s.Family} ×{s.Strength} — {s.Activity}";
                    bool manual = s.Autonomy == Cmd.AutonomyLevel.Manual;
                    r.BtnLabel.text = manual ? "MANUAL" : "AUTO";
                    r.BtnImg.color = manual ? _theme.Accent : _theme.ButtonIdle;
                    r.Go.SetActive(true);
                    _squadRows[i] = r;
                }
                else r.Go.SetActive(false);
            }
        }

        // Build rows: "name [contents] · cost" + a BUY button (greyed when unaffordable). Pooled.
        private void RenderBuildRows(Cmd.ConvoyCatalog catalog, float funds)
        {
            var opts = catalog?.Options;
            int count = opts?.Count ?? 0;
            EnsureEntityRows(_buildRows, _buildContainer, System.Math.Min(count, 6), "Build",
                id => _onBuyConvoy?.Invoke(id));
            for (int i = 0; i < _buildRows.Count; i++)
            {
                var r = _buildRows[i];
                if (opts != null && i < count && i < 6)
                {
                    var o = opts[i];
                    r.Id = o.Name;
                    string contents = string.IsNullOrEmpty(o.Contents) ? "" : $" [{o.Contents}]";
                    r.Label.text = $"{o.Name}{contents} · {o.Cost:0}";
                    bool afford = funds >= o.Cost;
                    r.BtnLabel.text = "BUY";
                    r.BtnImg.color = afford ? _theme.Accent : _theme.ButtonIdle;
                    r.Go.SetActive(true);
                    _buildRows[i] = r;
                }
                else r.Go.SetActive(false);
            }
        }

        // Build/grow a pool of generic label+button rows in a container; button calls onClick(row.Id).
        private void EnsureEntityRows(List<EntityRow> pool, Transform container, int count, string tag,
            Action<string> onClick)
        {
            while (pool.Count < count)
            {
                var row = UiFactory.HorizontalLayout(tag + "Row" + pool.Count, container, 4f);
                UiFactory.PreferredHeight(row.gameObject, 18f);
                var label = UiFactory.Label("L", row.transform, "", 12f, _theme.Text);
                var btn = UiFactory.Button("B", row.transform, "", _theme, null);
                var le = btn.gameObject.GetComponent<LayoutElement>() ?? btn.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 64f; le.flexibleWidth = 0f;
                int idx = pool.Count;
                btn.onClick.AddListener(() => { var id = pool[idx].Id; if (id != null) onClick?.Invoke(id); });
                pool.Add(new EntityRow { Go = row.gameObject, Label = label, BtnImg = btn.GetComponent<Image>(),
                    BtnLabel = btn.GetComponentInChildren<TextMeshProUGUI>() });
            }
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
                    _status.text = $"{armed.Value} -> {n} will respond: {names}";
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

        // Interactive per-operation rows in the HQ section: label + an AUTO/MANUAL toggle that takes that one
        // operation off the AI (or hands it back). Pooled + index-captured like the order rows.
        private void RenderOpRows(IReadOnlyList<CommanderLayer.Core.Command.OperationView> ops)
        {
            int count = ops?.Count ?? 0;
            EnsureOpRows(System.Math.Min(count, 5)); // cap visible op rows
            for (int i = 0; i < _opRows.Count; i++)
            {
                if (ops != null && i < count && i < 5)
                {
                    var op = ops[i];
                    var r = _opRows[i];
                    r.OpId = op.Id;
                    r.Label.text = $"{op.Kind} — {op.Phase} [{op.Status}]";
                    bool manual = op.Autonomy == CommanderLayer.Core.Command.AutonomyLevel.Manual;
                    r.BtnLabel.text = manual ? "MANUAL" : "AUTO";
                    r.BtnImg.color = manual ? _theme.Accent : _theme.ButtonIdle;
                    r.Go.SetActive(true);
                    _opRows[i] = r;
                }
                else
                {
                    _opRows[i].Go.SetActive(false);
                }
            }
        }

        private void EnsureOpRows(int count)
        {
            while (_opRows.Count < count)
            {
                var row = UiFactory.HorizontalLayout("OpRow" + _opRows.Count, _opsContainer, 4f);
                UiFactory.PreferredHeight(row.gameObject, 18f);
                var label = UiFactory.Label("L", row.transform, "", 12f, _theme.Text);
                var btn = UiFactory.Button("Auto", row.transform, "AUTO", _theme, null);
                var le = btn.gameObject.GetComponent<LayoutElement>() ?? btn.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 62f; le.flexibleWidth = 0f;
                int idx = _opRows.Count;
                btn.onClick.AddListener(() => { var id = _opRows[idx].OpId; if (id != null) _onToggleOpManual?.Invoke(id); });
                _opRows.Add(new OpRow { Go = row.gameObject, Label = label, BtnImg = btn.GetComponent<Image>(),
                    BtnLabel = btn.GetComponentInChildren<TextMeshProUGUI>() });
            }
        }

        private void EnsureRows(int count)
        {
            while (_rows.Count < count)
            {
                var row = UiFactory.HorizontalLayout("OrderRow" + _rows.Count, _ordersContainer, 4f);
                UiFactory.PreferredHeight(row.gameObject, 18f);
                var label = UiFactory.Label("L", row.transform, "", 12f, _theme.Text);
                var clearBtn = UiFactory.Button("X", row.transform, "X", _theme, null);
                var le = clearBtn.gameObject.GetComponent<LayoutElement>() ?? clearBtn.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 22f; le.flexibleWidth = 0f;
                int idx = _rows.Count;
                clearBtn.onClick.AddListener(() => { var id = _rows[idx].OrderId; if (id != null) _onClearOrder?.Invoke(id); });
                _rows.Add(new RowWidgets { Go = row.gameObject, Label = label, Clear = clearBtn });
            }
        }
    }
}
