using System;
using System.Collections.Generic;
using System.Linq;
using Nucleus.Core.Model;
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
        private Transform _assignContainer;
        private TextMeshProUGUI _assignHdr;
        private readonly List<EntityRow> _assignRows = new List<EntityRow>();
        private readonly Action<string, int> _onNudgePriority;
        private readonly Action<string> _onCycleKind;
        private Transform _objContainer;
        private TextMeshProUGUI _objHint, _objEditor;
        private readonly List<EntityRow> _objRows = new List<EntityRow>();
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
            Objectives = 1 << 7,  // drop-then-edit-in-place objective palette + list + editor
            All = Objectives | Mode | Operations | Squads | Build | Feed,
        }

        private readonly PanelSections _sections;

        public CommanderPanel(Transform parent, Theme theme, Action<bool> onSetAiCommander = null,
            Action<bool> onSetAutoFill = null, Action<string> onToggleOpManual = null,
            Action<string> onToggleSquadManual = null, Action<string> onBuyConvoy = null,
            Action<Cmd.ObjectiveKind> onArmObjective = null, Action<string> onSelectObjective = null,
            Action<string> onRemoveObjective = null, Action<string, int> onNudgePriority = null,
            Action<string> onCycleKind = null, Action<string, string> onAssignSquad = null,
            PanelSections sections = PanelSections.All)
        {
            _sections = sections;
            _theme = theme;
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

                _objContainer = UiFactory.VerticalLayout("ObjList", layout.transform, 3f, new RectOffset(0, 0, 0, 0)).transform;

                _objEditor = UiFactory.Label("ObjEditor", layout.transform, "", 11f, theme.Text);
                UiFactory.PreferredHeight(_objEditor.gameObject, 18f);
                var eRow = UiFactory.HorizontalLayout("ObjEdit", layout.transform, 4f);
                UiFactory.PreferredHeight(eRow.gameObject, 28f);
                UiFactory.Button("PrioDown", eRow.transform, "PRIO -", theme, () => { if (_selectedObjectiveId != null) _onNudgePriority?.Invoke(_selectedObjectiveId, -1); });
                UiFactory.Button("PrioUp", eRow.transform, "PRIO +", theme, () => { if (_selectedObjectiveId != null) _onNudgePriority?.Invoke(_selectedObjectiveId, +1); });
                UiFactory.Button("Retype", eRow.transform, "RETYPE", theme, () => { if (_selectedObjectiveId != null) _onCycleKind?.Invoke(_selectedObjectiveId); });
                var removeBtn = UiFactory.Button("ObjRemove", eRow.transform, "REMOVE", theme, () => { if (_selectedObjectiveId != null) { _onRemoveObjective?.Invoke(_selectedObjectiveId); _selectedObjectiveId = null; } });
                if (removeBtn.image != null) removeBtn.image.color = theme.Danger; // destructive = the ONLY red

                _assignHdr = UiFactory.Label("AssignHdr", layout.transform, "", 11f, theme.Accent);
                UiFactory.PreferredHeight(_assignHdr.gameObject, 16f);
                _assignContainer = UiFactory.VerticalLayout("AssignList", layout.transform, 3f, new RectOffset(0, 0, 0, 0)).transform;
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
            // Bars read as "distance to defeat": score over the starting pool (WarScore starts at 1000 and only
            // falls), so a half-full bar means half the war's attrition budget is gone.
            const float denom = 1000f;

            _scoreBlu.text = $"{b.BluforName} [{(b.BluforAi ? "AI" : "YOU")}]  {b.BluforScore:0}  ·  ${b.BluforFunds:0}  ·  -{b.BluforUnitsLost}u/-{b.BluforBasesLost}b";
            _scoreOp.text = $"{b.OpforName} [{(b.OpforAi ? "AI" : "YOU")}]  {b.OpforScore:0}  ·  ${b.OpforFunds:0}  ·  -{b.OpforUnitsLost}u/-{b.OpforBasesLost}b";
            SetBar(_scoreBluBar, b.BluforScore / denom);
            SetBar(_scoreOpBar, b.OpforScore / denom);

            if (b.Over)
            {
                _scoreStatus.text = b.WinnerName != null ? $"WAR OVER — {b.WinnerName} WINS" : UiStrings.WarOverDraw;
                _scoreStatus.color = _theme.Active;
            }
            else
            {
                _scoreStatus.text = UiStrings.WarInProgress;
                _scoreStatus.color = _theme.Muted;
            }
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

        /// <summary>Render the objective palette, live objective list, and selected-objective editor from the same
        /// operations read-model the map markers use, so panel and map agree.</summary>
        public void RenderObjectives(Cmd.HqSnapshot hq)
        {
            if (_objContainer == null) return;

            foreach (var kb in _kindButtons)
                kb.Img.color = _armedObjective == kb.Kind ? _theme.Active : _theme.ButtonIdle;

            if (_objHint != null)
            {
                _objHint.text = _armedObjective.HasValue
                    ? $"Drop {ObjectiveVisuals.Name(_armedObjective.Value)}: click a spot on the map."
                    : UiStrings.ObjectivesHintArmedPrompt;
            }

            var ops = hq?.Operations;
            int count = ops?.Count ?? 0;
            EnsureEntityRows(_objRows, _objContainer, System.Math.Min(count, 8), "Obj",
                id => { _selectedObjectiveId = id; _onSelectObjective?.Invoke(id); });
            for (int i = 0; i < _objRows.Count; i++)
            {
                var r = _objRows[i];
                if (ops != null && i < count && i < 8)
                {
                    var op = ops[i];
                    r.Id = op.ObjectiveId;
                    bool sel = op.ObjectiveId == _selectedObjectiveId;
                    string owner = op.PlayerOwned ? "you" : "AI";
                    r.Label.text = $"{(sel ? "▸ " : "")}{Dot(op.Kind)}{ObjectiveVisuals.Name(op.Kind)} · {ObjectiveVisuals.PhaseLabel(op.Phase)} · {op.SquadCount} sq [{owner}]";
                    r.Label.color = sel ? _theme.Active : _theme.Text;
                    r.BtnLabel.text = "SELECT";
                    r.BtnImg.color = sel ? _theme.Active : _theme.ButtonIdle;
                    r.Go.SetActive(true);
                    _objRows[i] = r;
                }
                else r.Go.SetActive(false);
            }

            if (_objEditor != null)
            {
                if (_selectedObjectiveId == null) { _objEditor.text = UiStrings.NoObjectiveSelected; }
                else
                {
                    string text = "Editing selected objective.";
                    if (ops != null)
                        foreach (var o in ops)
                            if (o.ObjectiveId == _selectedObjectiveId)
                            {
                                string owner = o.PlayerOwned ? "yours" : "AI";
                                text = $"{ObjectiveVisuals.Name(o.Kind)} · {ObjectiveVisuals.PhaseLabel(o.Phase)} · {o.SquadCount} squad{(o.SquadCount == 1 ? "" : "s")} · {owner} · Priority {o.Priority:0.#} (PRIO -/+)";
                                break;
                            }
                    _objEditor.text = text;
                }
            }

            RenderAssignList(hq);
        }

        // Free, suitable squads for the selected objective, each with an ASSIGN button. (Release is the squad
        // card's AI/YOU toggle, not here.)
        private void RenderAssignList(Cmd.HqSnapshot hq)
        {
            if (_assignContainer == null) return;
            var ops = hq?.Operations;
            var squads = hq?.Squads;

            Cmd.ObjectiveKind? selKind = null;
            if (_selectedObjectiveId != null && ops != null)
                foreach (var o in ops) if (o.ObjectiveId == _selectedObjectiveId) { selKind = o.Kind; break; }

            if (selKind == null || squads == null)
            {
                if (_assignHdr != null) _assignHdr.text = "";
                foreach (var r in _assignRows) r.Go.SetActive(false);
                return;
            }

            var suitable = Cmd.Families.SuitableFor(selKind.Value);
            var candidates = new List<Cmd.SquadView>();
            foreach (var s in squads)
                if (string.IsNullOrEmpty(s.AssignedOperationId) && suitable.Contains(s.Family))
                    candidates.Add(s);

            if (_assignHdr != null)
                _assignHdr.text = candidates.Count > 0 ? "ASSIGN FORCE → selected objective" : "ASSIGN FORCE — no free suitable squads";

            int shown = System.Math.Min(candidates.Count, 4);
            EnsureEntityRows(_assignRows, _assignContainer, shown, "Assign",
                squadId => { if (_selectedObjectiveId != null) _onAssignSquad?.Invoke(_selectedObjectiveId, squadId); });
            for (int i = 0; i < _assignRows.Count; i++)
            {
                var r = _assignRows[i];
                if (i < shown)
                {
                    var s = candidates[i];
                    r.Id = s.Id;
                    string comp = !string.IsNullOrEmpty(s.Composition) ? s.Composition : $"{s.Family} ×{s.Strength}";
                    r.Label.text = $"{s.Name} · {comp}";
                    r.BtnLabel.text = "ASSIGN";
                    r.BtnImg.color = _theme.Active;
                    r.Go.SetActive(true);
                    _assignRows[i] = r;
                }
                else r.Go.SetActive(false);
            }
        }

        /// <summary>Render the two command toggles + the HQ readout.</summary>
        public void RenderHq(Cmd.HqSnapshot hq, Cmd.ConvoyCatalog catalog, float funds)
        {
            bool running = hq != null;

            if (_aiCmdImg != null && hq != null)
            {
                _aiCommanderOn = hq.AiCreatesObjectives;
                _autoFillOn = hq.AiAutoFill;
                _aiCmdImg.color = _aiCommanderOn ? _theme.Active : _theme.ButtonIdle;
                _autoFillImg.color = _autoFillOn ? _theme.Active : _theme.ButtonIdle;
                if (_aiCmdLabel != null) _aiCmdLabel.text = _aiCommanderOn ? "AI COMMANDER: ON" : "AI COMMANDER: OFF";
                if (_autoFillLabel != null) _autoFillLabel.text = _autoFillOn ? "AI AUTO-FILL: ON" : "AI AUTO-FILL: OFF";
            }

            // Affordability is net of already-queued spend, so the BUY tint agrees with the "After" warning below.
            if (_buildContainer != null) RenderBuildRows(catalog, funds, hq?.QueuedCost ?? 0f);
            if (_buildFunds != null)
            {
                float after = funds - hq.QueuedCost;
                _buildFunds.text = $"Funds: {funds:0}  ·  Queued: {hq.QueuedCost:0}  ·  After: {after:0}";
                _buildFunds.color = after < 0f ? _theme.WarnText : _theme.Accent;
            }
            if (_buildStatus != null)
            {
                var sb = new System.Text.StringBuilder();
                if (hq != null)
                {
                    foreach (var line in hq.Production.Take(3)) sb.AppendLine(line);
                    foreach (var e in hq.Recent)
                        if (e.Kind == Cmd.ReportKind.ProductionQueued) { sb.AppendLine("· " + e.Text); break; }
                }
                _buildStatus.text = sb.Length > 0 ? sb.ToString().TrimEnd()
                    : UiStrings.NoOrdersInProgress;
            }

            if (_opsContainer != null) RenderOpRows(running ? hq.Operations : null);
            if (_squadsContainer != null) RenderSquadRows(running ? hq.Squads : null);

            if (_hqBody != null)
            {
                if (!running) { _hqBody.text = ""; }
                else
                {
                    var sb = new System.Text.StringBuilder();
                    foreach (var line in hq.Production.Take(3)) sb.AppendLine(line);
                    foreach (var e in hq.Recent.Take(5)) sb.AppendLine($"· {e.Text}");
                    _hqBody.text = sb.ToString().TrimEnd();
                }
            }
        }

        private void RenderSquadRows(System.Collections.Generic.IReadOnlyList<Cmd.SquadView> squads)
        {
            int count = squads?.Count ?? 0;
            if (_squadsEmpty != null) _squadsEmpty.gameObject.SetActive(count == 0);
            EnsureEntityRows(_squadRows, _squadsContainer, System.Math.Min(count, 6), "Squad",
                id => _onToggleSquadManual?.Invoke(id));
            for (int i = 0; i < _squadRows.Count; i++)
            {
                var r = _squadRows[i];
                if (squads != null && i < count && i < 6)
                {
                    var s = squads[i];
                    r.Id = s.Id;
                    string comp = !string.IsNullOrEmpty(s.Composition) ? s.Composition : $"{s.Family} ×{s.Strength}";
                    string need = s.TargetStrength > s.Strength ? $" ({s.Strength}/{s.TargetStrength})" : "";
                    r.Label.text = $"{s.Name} · {comp}{need} — {s.Activity}";
                    r.Label.color = s.Depleted ? _theme.WarnText : _theme.Text;
                    bool manual = s.Autonomy == Cmd.AutonomyLevel.Manual;
                    r.BtnLabel.text = manual ? "YOU" : "AI";
                    r.BtnImg.color = manual ? _theme.Accent : _theme.Active;
                    r.Go.SetActive(true);
                    _squadRows[i] = r;
                }
                else r.Go.SetActive(false);
            }
        }

        private void RenderBuildRows(Cmd.ConvoyCatalog catalog, float funds, float queuedCost)
        {
            var opts = catalog?.Options;
            int count = opts?.Count ?? 0;
            if (_buildEmpty != null) _buildEmpty.gameObject.SetActive(count == 0);
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
                    bool afford = (funds - queuedCost) >= o.Cost;
                    r.BtnLabel.text = "BUY";
                    r.BtnImg.color = afford ? _theme.Active : _theme.ButtonIdle;   // green = go, gray = can't afford
                    var buyBtn = r.BtnImg.GetComponent<Button>();
                    if (buyBtn != null) buyBtn.interactable = afford;              // can't click into debt
                    r.Go.SetActive(true);
                    _buildRows[i] = r;
                }
                else r.Go.SetActive(false);
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

        private void RenderOpRows(IReadOnlyList<Nucleus.Core.Command.OperationView> ops)
        {
            int count = ops?.Count ?? 0;
            if (_opsEmpty != null) _opsEmpty.gameObject.SetActive(count == 0);
            EnsureOpRows(System.Math.Min(count, 5));
            for (int i = 0; i < _opRows.Count; i++)
            {
                if (ops != null && i < count && i < 5)
                {
                    var op = ops[i];
                    var r = _opRows[i];
                    r.OpId = op.Id;
                    r.Label.text = $"{Dot(op.Kind)}{ObjectiveVisuals.Name(op.Kind)} — {ObjectiveVisuals.PhaseLabel(op.Phase)} [{ObjectiveVisuals.StatusLabel(op.Status)}]";
                    bool manual = op.Autonomy == Nucleus.Core.Command.AutonomyLevel.Manual;
                    r.BtnLabel.text = manual ? "YOU" : "AI";
                    r.BtnImg.color = manual ? _theme.Accent : _theme.Active;
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
                var (go, label, btn) = BuildRow(_opsContainer, "OpRow" + _opRows.Count, "AUTO", 62f);
                int idx = _opRows.Count;
                btn.onClick.AddListener(() => { var id = _opRows[idx].OpId; if (id != null) _onToggleOpManual?.Invoke(id); });
                _opRows.Add(new OpRow { Go = go, Label = label, BtnImg = btn.GetComponent<Image>(),
                    BtnLabel = btn.GetComponentInChildren<TextMeshProUGUI>() });
            }
        }
    }
}
