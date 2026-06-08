using System;
using System.Collections.Generic;
using System.Linq;
using Nucleus.Core.Model;
using Nucleus.Presentation;
using Cmd = Nucleus.Core.Command;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nucleus.Ui
{
    /// <summary>The shared commander panel each mod renders its slice of, selected via <see cref="PanelSections"/>.
    /// Pure presentation over the campaign read-models — no game access.</summary>
    public sealed class CommanderPanel
    {
        private readonly Theme _theme;
        private readonly RectTransform _root;
        private readonly TextMeshProUGUI _hqHeader;
        private readonly TextMeshProUGUI _hqBody;
        private Image _aiCmdImg, _autoFillImg;
        private TextMeshProUGUI _aiCmdLabel, _autoFillLabel;
        private bool _aiCommanderOn = true, _autoFillOn = true;
        private readonly Transform _opsContainer;
        private readonly Action<string> _onToggleOpManual;
        private readonly Action<string> _onToggleSquadManual;
        private readonly Action<string> _onBuyConvoy;
        private readonly Action<Cmd.ObjectiveKind> _onArmObjective;
        private readonly Action<string> _onSelectObjective;
        private readonly Action<string> _onRemoveObjective;
        private readonly Action<string, string> _onAssignSquad;   // (objectiveId, squadId)
        private readonly Action<string, int> _onNudgePriority;
        private readonly Action<string> _onCycleKind;
        private TextMeshProUGUI _objHint;
        // Command-center order tree + selection detail (replaces the stacked objective/operation lists).
        private readonly Action<string> _onToggleOrderManual;   // (selected objectiveId) -> take its order over / release
        private Transform _treeContainer;
        private TextMeshProUGUI _treeEmpty;
        private readonly List<EntityRow> _treeRows = new List<EntityRow>();
        private TextMeshProUGUI _detailTitle, _detailStatus, _detailForce, _detailActionLabel;
        private Image _detailActionImg;
        private GameObject _detailAction;
        private readonly List<KindButton> _kindButtons = new List<KindButton>();
        private struct KindButton { public Image Img; public Cmd.ObjectiveKind Kind; }
        private Cmd.ObjectiveKind? _armedObjective;   // the kind the player is about to drop
        private string _selectedObjectiveId;          // the objective currently being edited
        private readonly List<OpRow> _opRows = new List<OpRow>();
        private readonly Transform _squadsContainer;
        private readonly List<EntityRow> _squadRows = new List<EntityRow>();
        private readonly Transform _buildContainer;
        private readonly List<EntityRow> _buildRows = new List<EntityRow>();
        private TextMeshProUGUI _buildEmpty, _squadsEmpty, _opsEmpty;
        private TextMeshProUGUI _buildFunds, _buildStatus;
        private TextMeshProUGUI _scoreTitle, _scoreBlu, _scoreOp, _scoreStatus;
        private Image _scoreBluBar, _scoreOpBar;

        private struct OpRow { public GameObject Go; public TextMeshProUGUI Label; public Image BtnImg; public TextMeshProUGUI BtnLabel; public string OpId; }
        private struct EntityRow { public GameObject Go; public TextMeshProUGUI Label; public Image BtnImg; public TextMeshProUGUI BtnLabel; public string Id; }

        public RectTransform Root => _root;

        /// <summary>Which sections the panel builds — so each mod renders only its slice (CMD = Orders|Mode,
        /// Build = Build, Squad = Squads, Warfare = Operations|Feed).</summary>
        [Flags]
        public enum PanelSections
        {
            None = 0,
            Mode = 1 << 1,        // commander mode selector + confirm
            Operations = 1 << 2,
            Squads = 1 << 3,
            Build = 1 << 4,
            Feed = 1 << 5,
            Scoreboard = 1 << 6,  // dynamic-war attrition board: both factions' score/funds/losses + win state
            Objectives = 1 << 7,  // drop palette + command-center order tree + selection-detail pane
            All = Objectives | Mode | Operations | Squads | Build | Feed,
        }

        private readonly PanelSections _sections;

        public CommanderPanel(Transform parent, Theme theme, Action<bool> onSetAiCommander = null,
            Action<bool> onSetAutoFill = null, Action<string> onToggleOpManual = null,
            Action<string> onToggleSquadManual = null, Action<string> onBuyConvoy = null,
            Action<Cmd.ObjectiveKind> onArmObjective = null, Action<string> onSelectObjective = null,
            Action<string> onRemoveObjective = null, Action<string, int> onNudgePriority = null,
            Action<string> onCycleKind = null, Action<string, string> onAssignSquad = null,
            Action<string> onToggleOrderManual = null,
            PanelSections sections = PanelSections.All)
        {
            _sections = sections;
            _theme = theme;
            _onToggleOrderManual = onToggleOrderManual;
            _onToggleOpManual = onToggleOpManual;
            _onToggleSquadManual = onToggleSquadManual;
            _onBuyConvoy = onBuyConvoy;
            _onArmObjective = onArmObjective;
            _onSelectObjective = onSelectObjective;
            _onRemoveObjective = onRemoveObjective;
            _onAssignSquad = onAssignSquad;
            _onNudgePriority = onNudgePriority;
            _onCycleKind = onCycleKind;
            _root = UiFactory.Panel("CommanderPanel", parent, theme.PanelBackground);
            // Clipped viewport + content column sized to its children, so sections extend and scroll, never compress.
            var viewport = UiFactory.Panel("Viewport", _root, theme.Transparent);
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
            // Permanent scrollbar — wheel-only scrolling was undiscoverable.
            scroll.verticalScrollbar = UiFactory.VerticalScrollbar(_root, theme);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            bool Has(PanelSections s) => (_sections & s) != 0;

            if (Has(PanelSections.Objectives))
            {
                UiFactory.SectionHeader(layout.transform, UiStrings.ObjectivesHeader, theme);

                var pRow1 = UiFactory.HorizontalLayout("ObjPalette1", layout.transform, 4f);
                UiFactory.PreferredHeight(pRow1.gameObject, 28f);
                AddKindButton(pRow1.transform, "CAPTURE", Cmd.ObjectiveKind.CapturePoint);
                AddKindButton(pRow1.transform, "DESTROY", Cmd.ObjectiveKind.DestroyTarget);
                AddKindButton(pRow1.transform, "DEFEND", Cmd.ObjectiveKind.DefendArea);
                var pRow2 = UiFactory.HorizontalLayout("ObjPalette2", layout.transform, 4f);
                UiFactory.PreferredHeight(pRow2.gameObject, 28f);
                AddKindButton(pRow2.transform, "AIRSPACE", Cmd.ObjectiveKind.ControlAirspace);
                AddKindButton(pRow2.transform, "RECON", Cmd.ObjectiveKind.Recon);
                AddKindButton(pRow2.transform, "RESUPPLY", Cmd.ObjectiveKind.Resupply);

                _objHint = UiFactory.Label("ObjHint", layout.transform, UiStrings.ObjectivesHint, 11f, theme.Muted);
                UiFactory.PreferredHeight(_objHint.gameObject, 30f);

                // The command-center order tree: a parent row per goal + indented prerequisite rows, each
                // selectable; the AI/YOU badge doubles as the select target (per-row click).
                UiFactory.SectionHeader(layout.transform, UiStrings.OrdersHeader, theme);
                UiFactory.PreferredHeight(UiFactory.Label("OrdersHint", layout.transform,
                    UiStrings.OrdersTreeHint, 11f, theme.Muted).gameObject, 30f);
                _treeContainer = UiFactory.VerticalLayout("OrderTree", layout.transform, 2f, new RectOffset(0, 0, 0, 0)).transform;
                _treeEmpty = UiFactory.Label("OrdersEmpty", layout.transform, UiStrings.OrdersEmpty, 12f, theme.Muted);
                UiFactory.PreferredHeight(_treeEmpty.gameObject, 48f);

                // Selection detail pane for the picked node: title (kind), status, force, live phase, and the
                // primary Take Over / Release action bound to ICampaign.ToggleOrderManual.
                UiFactory.Divider(layout.transform, theme.Muted);
                _detailTitle = UiFactory.Label("DetailTitle", layout.transform, UiStrings.NoNodeSelected, 13f, theme.Muted);
                UiFactory.PreferredHeight(_detailTitle.gameObject, 20f);
                _detailStatus = UiFactory.Label("DetailStatus", layout.transform, "", 12f, theme.Text);
                UiFactory.PreferredHeight(_detailStatus.gameObject, 18f);
                _detailForce = UiFactory.Label("DetailForce", layout.transform, "", 11f, theme.Muted);
                UiFactory.PreferredHeight(_detailForce.gameObject, 16f);
                var takeOverBtn = UiFactory.Button("OrderTakeOver", layout.transform, "Take Over", theme,
                    () => { if (_selectedObjectiveId != null) _onToggleOrderManual?.Invoke(_selectedObjectiveId); });
                UiFactory.PreferredHeight(takeOverBtn.gameObject, 28f);
                _detailAction = takeOverBtn.gameObject;
                _detailActionImg = takeOverBtn.GetComponent<Image>();
                _detailActionLabel = takeOverBtn.GetComponentInChildren<TextMeshProUGUI>();
                _detailAction.SetActive(false);
            }

            if (Has(PanelSections.Mode))
            {
                _hqHeader = UiFactory.SectionHeader(layout.transform, UiStrings.CommanderHeader, theme);

                var aiCmdBtn = UiFactory.Button("AiCommander", layout.transform, "AI COMMANDER", theme,
                    () => onSetAiCommander?.Invoke(!_aiCommanderOn));
                UiFactory.PreferredHeight(aiCmdBtn.gameObject, 28f);
                _aiCmdImg = aiCmdBtn.GetComponent<Image>();
                _aiCmdLabel = aiCmdBtn.GetComponentInChildren<TextMeshProUGUI>();

                var autoFillBtn = UiFactory.Button("AutoFill", layout.transform, "AI AUTO-FILL", theme,
                    () => onSetAutoFill?.Invoke(!_autoFillOn));
                UiFactory.PreferredHeight(autoFillBtn.gameObject, 28f);
                _autoFillImg = autoFillBtn.GetComponent<Image>();
                _autoFillLabel = autoFillBtn.GetComponentInChildren<TextMeshProUGUI>();

                UiFactory.PreferredHeight(UiFactory.Label("ToggleHint", layout.transform,
                    UiStrings.ModeHint, 11f, theme.Muted).gameObject, 44f);
            }

            if (Has(PanelSections.Operations))
            {
                UiFactory.SectionHeader(layout.transform, UiStrings.OperationsHeader, theme);
                UiFactory.PreferredHeight(UiFactory.Label("OpsHint", layout.transform,
                    UiStrings.OperationsHint, 11f, theme.Muted).gameObject, 30f);
                _opsContainer = UiFactory.VerticalLayout("HqOps", layout.transform, 3f, new RectOffset(0, 0, 0, 0)).transform;
                _opsEmpty = UiFactory.Label("OpsEmpty", layout.transform, UiStrings.OpsEmpty, 12f, theme.Muted);
                UiFactory.PreferredHeight(_opsEmpty.gameObject, 48f);
            }

            if (Has(PanelSections.Squads))
            {
                UiFactory.SectionHeader(layout.transform, UiStrings.SquadsHeader, theme);
                UiFactory.PreferredHeight(UiFactory.Label("SquadsHint", layout.transform,
                    UiStrings.SquadsHint, 11f, theme.Muted).gameObject, 30f);
                _squadsContainer = UiFactory.VerticalLayout("HqSquads", layout.transform, 3f, new RectOffset(0, 0, 0, 0)).transform;
                _squadsEmpty = UiFactory.Label("SquadsEmpty", layout.transform, UiStrings.SquadsEmpty, 12f, theme.Muted);
                UiFactory.PreferredHeight(_squadsEmpty.gameObject, 48f);
            }

            if (Has(PanelSections.Build))
            {
                UiFactory.SectionHeader(layout.transform, UiStrings.BuildHeader, theme);
                UiFactory.PreferredHeight(UiFactory.Label("BuildAircraft", layout.transform,
                    UiStrings.BuildAircraftNote, 11f, theme.Muted).gameObject, 16f);
                UiFactory.PreferredHeight(UiFactory.Label("BuildHint", layout.transform,
                    UiStrings.BuildHint, 11f, theme.Muted).gameObject, 56f);
                _buildFunds = UiFactory.Label("BuildFunds", layout.transform, UiStrings.FundsPlaceholder, 12f, theme.Accent);
                UiFactory.PreferredHeight(_buildFunds.gameObject, 18f);
                _buildContainer = UiFactory.VerticalLayout("HqBuild", layout.transform, 3f, new RectOffset(0, 0, 0, 0)).transform;
                _buildEmpty = UiFactory.Label("BuildEmpty", layout.transform, UiStrings.BuildEmpty, 12f, theme.Muted);
                UiFactory.PreferredHeight(_buildEmpty.gameObject, 36f);
                UiFactory.PreferredHeight(UiFactory.Label("BuildQHdr", layout.transform, UiStrings.OrdersHeader, 11f, theme.Muted).gameObject, 16f);
                _buildStatus = UiFactory.Label("BuildStatus", layout.transform, UiStrings.NoOrders, 11f, theme.Text);
                UiFactory.PreferredHeight(_buildStatus.gameObject, 64f);
            }

            if (Has(PanelSections.Scoreboard))
            {
                UiFactory.SectionHeader(layout.transform, UiStrings.AttritionHeader, theme);
                _scoreTitle = UiFactory.Label("ScoreTitle", layout.transform,
                    UiStrings.AttritionHint, 11f, theme.Muted);
                UiFactory.PreferredHeight(_scoreTitle.gameObject, 40f);

                _scoreBlu = UiFactory.Label("ScoreBlu", layout.transform, "BLUFOR", 13f, theme.ScoreBlufor);
                UiFactory.PreferredHeight(_scoreBlu.gameObject, 20f);
                _scoreBluBar = MakeBar("BluBar", layout.transform, theme.ScoreBlufor);

                _scoreOp = UiFactory.Label("ScoreOp", layout.transform, "OPFOR", 13f, theme.ScoreOpfor);
                UiFactory.PreferredHeight(_scoreOp.gameObject, 20f);
                _scoreOpBar = MakeBar("OpBar", layout.transform, theme.ScoreOpfor);

                _scoreStatus = UiFactory.Label("ScoreStatus", layout.transform, "", 12f, theme.Text);
                UiFactory.PreferredHeight(_scoreStatus.gameObject, 22f);
            }

            if (Has(PanelSections.Feed))
            {
                UiFactory.SectionHeader(layout.transform, UiStrings.FeedHeader, theme);
                _hqBody = UiFactory.Label("HqBody", layout.transform, "", 12f, theme.Muted);
                UiFactory.PreferredHeight(_hqBody.gameObject, 110f);
            }
        }

        // A thin progress bar: a dark track with a colored fill child whose right anchor encodes the fraction.
        private Image MakeBar(string name, Transform parent, Color fill)
        {
            var track = UiFactory.Panel(name + "Track", parent, _theme.BarTrack);
            UiFactory.PreferredHeight(track.gameObject, 12f);
            var bar = UiFactory.Panel(name + "Fill", track, fill);
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.offsetMin = Vector2.zero; bar.offsetMax = Vector2.zero;
            return bar.GetComponent<Image>();
        }

        public void RenderScoreboard(Cmd.WarfareCampaign.Scoreboard b)
        {
            if (_scoreBlu == null) return;
            var vm = PresentationBuilder.BuildScoreboard(b);
            _scoreBlu.text = vm.BluforLine;
            _scoreOp.text = vm.OpforLine;
            SetBar(_scoreBluBar, vm.BluforFraction);
            SetBar(_scoreOpBar, vm.OpforFraction);
            _scoreStatus.text = vm.Status;
            _scoreStatus.color = Resolve(vm.StatusColor, default);
        }

        private static void SetBar(Image bar, float fraction)
        {
            if (bar == null) return;
            float f = Mathf.Clamp01(fraction);
            var rt = bar.rectTransform;
            rt.anchorMax = new Vector2(f, 1f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        // ---- Objectives section ------------------------------------------------------------------------

        public Cmd.ObjectiveKind? ArmedObjective => _armedObjective;
        // Kind-colored bullet from ObjectiveVisuals (shared with the map markers, so they can't drift).
        private static string Dot(Cmd.ObjectiveKind kind) => $"<color=#{ObjectiveVisuals.Hex(kind)}>●</color> ";

        public string SelectedObjectiveId => _selectedObjectiveId;
        public void SetSelectedObjective(string id) => _selectedObjectiveId = id;
        public void ClearArmedObjective() => _armedObjective = null;

        private void AddKindButton(Transform parent, string label, Cmd.ObjectiveKind kind)
        {
            var btn = UiFactory.Button("Kind_" + kind, parent, label, _theme, () =>
            {
                _armedObjective = _armedObjective == kind ? (Cmd.ObjectiveKind?)null : kind;
                _onArmObjective?.Invoke(kind);
            });
            _kindButtons.Add(new KindButton { Img = btn.GetComponent<Image>(), Kind = kind });
        }

        /// <summary>Render the drop palette + the command-center order tree + the selection-detail pane from the
        /// pure presentation VMs. The panel keeps only UI-local arm/selection state.</summary>
        public void RenderObjectives(Cmd.HqSnapshot hq)
        {
            if (_treeContainer == null) return;

            foreach (var kb in _kindButtons)
                kb.Img.color = _armedObjective == kb.Kind ? _theme.Active : _theme.ButtonIdle;

            if (_objHint != null)
                _objHint.text = _armedObjective.HasValue
                    ? $"Drop {ObjectiveVisuals.Name(_armedObjective.Value)}: click a spot on the map."
                    : UiStrings.ObjectivesHintArmedPrompt;

            var tree = PresentationBuilder.BuildOrderTree(hq, _selectedObjectiveId);
            if (_treeEmpty != null) _treeEmpty.gameObject.SetActive(tree.Count == 0);
            EnsureEntityRows(_treeRows, _treeContainer, tree.Count, "Order",
                id => { _selectedObjectiveId = id; _onSelectObjective?.Invoke(id); });
            for (int i = 0; i < _treeRows.Count; i++)
            {
                if (i < tree.Count) { var r = _treeRows[i]; r.Id = tree[i].Id; ApplyTreeRow(r, tree[i]); _treeRows[i] = r; }
                else _treeRows[i].Go.SetActive(false);
            }

            RenderNodeDetail(hq);
        }

        // Apply one order-tree row: indent (parent=0, prerequisite=1) via TMP left-margin, kind dot, selection
        // color, dimmed when unreachable, and the AI/YOU badge on the row's select button.
        private void ApplyTreeRow(EntityRow r, OrderRowVm vm)
        {
            r.Label.margin = new Vector4(vm.Indent * 14f, 0f, 0f, 0f);
            r.Label.text = (vm.ShowKindDot ? Dot(vm.Kind) : "") + vm.Label;
            r.Label.color = Resolve(vm.LabelColor, vm.Kind);
            r.Label.alpha = vm.Unreachable ? 0.45f : 1f;
            r.BtnLabel.text = vm.Badge;
            r.BtnImg.color = vm.Selected ? _theme.Active : Resolve(vm.BadgeColor, vm.Kind);
            r.Go.SetActive(true);
        }

        // The selection-detail pane for the picked node: title/status/force + the Take Over / Release action.
        private void RenderNodeDetail(Cmd.HqSnapshot hq)
        {
            if (_detailTitle == null) return;
            var d = PresentationBuilder.BuildNodeDetail(hq, _selectedObjectiveId);
            if (!d.HasSelection)
            {
                _detailTitle.text = UiStrings.NoNodeSelected;
                _detailTitle.color = _theme.Muted;
                _detailTitle.alpha = 1f;
                _detailStatus.text = "";
                _detailForce.text = "";
                if (_detailAction != null) _detailAction.SetActive(false);
                return;
            }
            _detailTitle.text = Dot(d.Kind) + d.Title;
            _detailTitle.color = Resolve(d.TitleColor, d.Kind);
            _detailTitle.alpha = d.Unreachable ? 0.45f : 1f;
            _detailStatus.text = d.Status;
            _detailForce.text = d.Force;
            if (_detailActionLabel != null) _detailActionLabel.text = d.Action;
            if (_detailActionImg != null) _detailActionImg.color = Resolve(d.ActionColor, d.Kind);
            if (_detailAction != null) _detailAction.SetActive(true);
        }

        // Map a pure VM row list onto a pooled EntityRow list: grow the pool, apply each row, hide the rest.
        private void FillRows(List<EntityRow> pool, Transform container, IReadOnlyList<RowVm> rows, string tag, Action<string> onClick)
        {
            if (container == null) return;
            EnsureEntityRows(pool, container, rows.Count, tag, onClick);
            for (int i = 0; i < pool.Count; i++)
            {
                if (i < rows.Count) { var r = pool[i]; r.Id = rows[i].Id; ApplyRow(r, rows[i]); pool[i] = r; }
                else pool[i].Go.SetActive(false);
            }
        }

        private void ApplyRow(EntityRow r, RowVm vm)
        {
            r.Label.text = (vm.ShowKindDot ? Dot(vm.Kind) : "") + vm.Label;
            r.Label.color = Resolve(vm.LabelColor, vm.Kind);
            r.BtnLabel.text = vm.Button;
            r.BtnImg.color = Resolve(vm.ButtonColor, vm.Kind);
            var btn = r.BtnImg.GetComponent<Button>();
            if (btn != null) btn.interactable = vm.ButtonEnabled;
            r.Go.SetActive(true);
        }

        // Resolve a semantic VM color role to the concrete palette color (kind colors via ObjectiveVisuals).
        private Color Resolve(UiColor c, Cmd.ObjectiveKind kind)
        {
            switch (c)
            {
                case UiColor.Muted: return _theme.Muted;
                case UiColor.Active: return _theme.Active;
                case UiColor.Idle: return _theme.ButtonIdle;
                case UiColor.Accent: return _theme.Accent;
                case UiColor.Danger: return _theme.Danger;
                case UiColor.Warn: return _theme.WarnText;
                case UiColor.Kind: return ObjectiveVisuals.Color(kind);
                default: return _theme.Text;
            }
        }

        /// <summary>Render the two command toggles + ops/squads/build/feed from the pure <see cref="PanelVm"/>.</summary>
        public void RenderHq(Cmd.HqSnapshot hq, Cmd.ConvoyCatalog catalog, float funds)
        {
            var vm = PresentationBuilder.Build(hq, new PanelInteraction(_armedObjective, _selectedObjectiveId), catalog, funds);

            if (_aiCmdImg != null && hq != null)
            {
                _aiCommanderOn = vm.AiCommanderOn;
                _autoFillOn = vm.AiAutoFillOn;
                _aiCmdImg.color = _aiCommanderOn ? _theme.Active : _theme.ButtonIdle;
                _autoFillImg.color = _autoFillOn ? _theme.Active : _theme.ButtonIdle;
                if (_aiCmdLabel != null) _aiCmdLabel.text = _aiCommanderOn ? "AI COMMANDER: ON" : "AI COMMANDER: OFF";
                if (_autoFillLabel != null) _autoFillLabel.text = _autoFillOn ? "AI AUTO-FILL: ON" : "AI AUTO-FILL: OFF";
            }

            if (_buildContainer != null)
            {
                if (_buildEmpty != null) _buildEmpty.gameObject.SetActive(vm.BuildEmpty);
                FillRows(_buildRows, _buildContainer, vm.BuildRows, "Build", id => _onBuyConvoy?.Invoke(id));
            }
            if (_buildFunds != null) { _buildFunds.text = vm.BuildFunds; _buildFunds.color = Resolve(vm.BuildFundsColor, default); }
            if (_buildStatus != null) _buildStatus.text = vm.BuildStatus ?? UiStrings.NoOrdersInProgress;

            if (_opsContainer != null)
            {
                if (_opsEmpty != null) _opsEmpty.gameObject.SetActive(vm.OperationRows.Count == 0);
                FillOpRows(vm.OperationRows);
            }
            if (_squadsContainer != null)
            {
                if (_squadsEmpty != null) _squadsEmpty.gameObject.SetActive(vm.SquadsEmpty);
                FillRows(_squadRows, _squadsContainer, vm.SquadRows, "Squad", id => _onToggleSquadManual?.Invoke(id));
            }

            if (_hqBody != null) _hqBody.text = vm.Feed;
        }

        // Operations use their own pool struct (OpRow) + toggle wiring, so they get a parallel filler.
        private void FillOpRows(IReadOnlyList<RowVm> rows)
        {
            EnsureOpRows(rows.Count);
            for (int i = 0; i < _opRows.Count; i++)
            {
                if (i < rows.Count)
                {
                    var r = _opRows[i]; var vm = rows[i];
                    r.OpId = vm.Id;
                    r.Label.text = (vm.ShowKindDot ? Dot(vm.Kind) : "") + vm.Label;
                    r.Label.color = Resolve(vm.LabelColor, vm.Kind);
                    r.BtnLabel.text = vm.Button;
                    r.BtnImg.color = Resolve(vm.ButtonColor, vm.Kind);
                    r.Go.SetActive(true);
                    _opRows[i] = r;
                }
                else _opRows[i].Go.SetActive(false);
            }
        }

        // Shared scaffold for the pooled list builders: flexible label + fixed-width action button.
        private (GameObject go, TextMeshProUGUI label, Button btn) BuildRow(Transform container, string name, string btnText, float btnWidth)
        {
            var row = UiFactory.HorizontalLayout(name, container, 4f);
            UiFactory.PreferredHeight(row.gameObject, 18f);
            var label = UiFactory.Label("L", row.transform, "", 12f, _theme.Text);
            var btn = UiFactory.Button("B", row.transform, btnText, _theme, null);
            var le = btn.gameObject.GetComponent<LayoutElement>() ?? btn.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = btnWidth; le.flexibleWidth = 0f;
            return (row.gameObject, label, btn);
        }

        // Grow a pool of label+button rows; each button calls onClick(row.Id).
        private void EnsureEntityRows(List<EntityRow> pool, Transform container, int count, string tag,
            Action<string> onClick)
        {
            while (pool.Count < count)
            {
                var (go, label, btn) = BuildRow(container, tag + "Row" + pool.Count, "", 64f);
                int idx = pool.Count;
                btn.onClick.AddListener(() => { var id = pool[idx].Id; if (id != null) onClick?.Invoke(id); });
                pool.Add(new EntityRow { Go = go, Label = label, BtnImg = btn.GetComponent<Image>(),
                    BtnLabel = btn.GetComponentInChildren<TextMeshProUGUI>() });
            }
        }

        private void EnsureOpRows(int count)
        {
            while (_opRows.Count < count)
            {
                var (go, label, btn) = BuildRow(_opsContainer, "OpRow" + _opRows.Count, "AUTO", 62f);
                int idx = _opRows.Count;
                btn.onClick.AddListener(() => { var id = _opRows[idx].OpId; if (id != null) _onToggleOpManual?.Invoke(id); });
                _opRows.Add(new OpRow { Go = go, Label = label, BtnImg = btn.GetComponent<Image>(),
                    BtnLabel = btn.GetComponentInChildren<TextMeshProUGUI>() });
            }
        }
    }
}
