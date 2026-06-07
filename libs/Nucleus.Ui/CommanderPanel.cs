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
    /// <summary>
    /// The shared commander panel each mod renders its slice of (objectives, the two command toggles,
    /// operations, squads, build, the battle feed, and the dynamic-war scoreboard), selected via
    /// <see cref="PanelSections"/>. Pure presentation over the campaign read-models — no game access.
    /// </summary>
    public sealed class CommanderPanel
    {
        private readonly Theme _theme;
        private readonly RectTransform _root;
        private readonly TextMeshProUGUI _hqHeader;
        private readonly TextMeshProUGUI _hqBody;
        // The two command toggles (mod is always on).
        private Image _aiCmdImg, _autoFillImg;
        private TextMeshProUGUI _aiCmdLabel, _autoFillLabel;
        private bool _aiCommanderOn = true, _autoFillOn = true;
        private readonly Transform _opsContainer;
        private readonly Action<string> _onToggleOpManual;
        private readonly Action<string> _onToggleSquadManual;
        private readonly Action<string> _onBuyConvoy;
        // Objectives section (drop-then-edit-in-place).
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
        // Empty-state hints (shown when a section has no data, so a screen never looks blank/broken).
        private TextMeshProUGUI _buildEmpty, _squadsEmpty, _opsEmpty;
        // Build section feedback: current funds + the production queue / last order echo.
        private TextMeshProUGUI _buildFunds, _buildStatus;
        // Scoreboard widgets (dynamic-war attrition board).
        private TextMeshProUGUI _scoreTitle, _scoreBlu, _scoreOp, _scoreStatus;
        private Image _scoreBluBar, _scoreOpBar;
        private float _scoreMax;  // highest score seen — the 100%% bar reference (so bars shrink as sides attrit)
        private static readonly Color BluColor = new Color(0.35f, 0.6f, 1f, 1f);
        private static readonly Color OpColor = new Color(1f, 0.45f, 0.4f, 1f);

        private struct OpRow { public GameObject Go; public TextMeshProUGUI Label; public Image BtnImg; public TextMeshProUGUI BtnLabel; public string OpId; }
        // Generic interactive row: a label + an action button carrying an id (squad id / convoy name).
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
            // Visible, grabbable scrollbar — the panel often exceeds its height; without this the player can't
            // tell it scrolls (wheel-only was undiscoverable). Permanent so the affordance is always shown.
            scroll.verticalScrollbar = UiFactory.VerticalScrollbar(_root, theme);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            bool Has(PanelSections s) => (_sections & s) != 0;

            if (Has(PanelSections.Objectives))
            {
                // OBJECTIVES — the single command primitive. Pick a kind, click the map to DROP it, then select
                // a marker to EDIT in place (priority, retype, remove). The AI fills it with squads (or you do).
                UiFactory.SectionHeader(layout.transform, "OBJECTIVES — drop on the map", theme);

                // Palette: one button per objective kind; clicking arms that kind for the next map click.
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

                _objHint = UiFactory.Label("ObjHint", layout.transform, "Pick a kind, then click the map to drop an objective.", 11f, theme.Muted);
                UiFactory.PreferredHeight(_objHint.gameObject, 30f);

                // List of live objectives — each row selects (to edit in place); plus a per-row REMOVE.
                _objContainer = UiFactory.VerticalLayout("ObjList", layout.transform, 3f, new RectOffset(0, 0, 0, 0)).transform;

                // Editor for the selected objective: priority -/+, retype (cycle kind), remove.
                _objEditor = UiFactory.Label("ObjEditor", layout.transform, "", 11f, theme.Text);
                UiFactory.PreferredHeight(_objEditor.gameObject, 18f);
                var eRow = UiFactory.HorizontalLayout("ObjEdit", layout.transform, 4f);
                UiFactory.PreferredHeight(eRow.gameObject, 28f);
                UiFactory.Button("PrioDown", eRow.transform, "PRIO -", theme, () => { if (_selectedObjectiveId != null) _onNudgePriority?.Invoke(_selectedObjectiveId, -1); });
                UiFactory.Button("PrioUp", eRow.transform, "PRIO +", theme, () => { if (_selectedObjectiveId != null) _onNudgePriority?.Invoke(_selectedObjectiveId, +1); });
                UiFactory.Button("Retype", eRow.transform, "RETYPE", theme, () => { if (_selectedObjectiveId != null) _onCycleKind?.Invoke(_selectedObjectiveId); });
                var removeBtn = UiFactory.Button("ObjRemove", eRow.transform, "REMOVE", theme, () => { if (_selectedObjectiveId != null) { _onRemoveObjective?.Invoke(_selectedObjectiveId); _selectedObjectiveId = null; } });
                if (removeBtn.image != null) removeBtn.image.color = theme.Danger; // destructive = the ONLY red

                // ASSIGN FORCE — when an objective is selected, list free, suitable squads to hand it. So the
                // player can actually command ("assign this squad to that objective"), not only toggle autonomy.
                _assignHdr = UiFactory.Label("AssignHdr", layout.transform, "", 11f, theme.Accent);
                UiFactory.PreferredHeight(_assignHdr.gameObject, 16f);
                _assignContainer = UiFactory.VerticalLayout("AssignList", layout.transform, 3f, new RectOffset(0, 0, 0, 0)).transform;
            }

            if (Has(PanelSections.Mode))
            {
                // The mod is always on. Two toggles: who creates objectives (AI or you), and whether the AI
                // auto-fills objectives with squads (forms + recruits + assigns). Green when on.
                _hqHeader = UiFactory.SectionHeader(layout.transform, "COMMANDER", theme);

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
                    "AI COMMANDER: the AI creates objectives (off = only you do).  AI AUTO-FILL: the AI forms squads, recruits, and assigns them to objectives (off = you assign).",
                    11f, theme.Muted).gameObject, 44f);
            }

            if (Has(PanelSections.Operations))
            {
                // OPERATIONS — one interactive row per op with an AUTO/MANUAL toggle (take a slice).
                UiFactory.SectionHeader(layout.transform, "OPERATIONS", theme);
                UiFactory.PreferredHeight(UiFactory.Label("OpsHint", layout.transform,
                    "Each operation is run by AI (the commander sequences its phases) or YOU (manual). Tap the AI/YOU button to switch.",
                    11f, theme.Muted).gameObject, 30f);
                _opsContainer = UiFactory.VerticalLayout("HqOps", layout.transform, 3f, new RectOffset(0, 0, 0, 0)).transform;
                _opsEmpty = UiFactory.Label("OpsEmpty", layout.transform, "No operations running. Drop an objective on the map (or enable AI COMMANDER) and the squads will form up and fight.", 12f, theme.Muted);
                UiFactory.PreferredHeight(_opsEmpty.gameObject, 48f);
            }

            if (Has(PanelSections.Squads))
            {
                // SQUADS — name + what it's doing + an AUTO/MANUAL toggle (manage each squad).
                UiFactory.SectionHeader(layout.transform, "SQUADS", theme);
                UiFactory.PreferredHeight(UiFactory.Label("SquadsHint", layout.transform,
                    "Each squad is AI-run (the commander tasks it) or YOURS (you hold it for manual orders). Tap the AI/YOU button to switch.",
                    11f, theme.Muted).gameObject, 30f);
                _squadsContainer = UiFactory.VerticalLayout("HqSquads", layout.transform, 3f, new RectOffset(0, 0, 0, 0)).transform;
                _squadsEmpty = UiFactory.Label("SquadsEmpty", layout.transform, "No squads yet. Squads form automatically from your forces as the war starts.", 12f, theme.Muted);
                UiFactory.PreferredHeight(_squadsEmpty.gameObject, 48f);
            }

            if (Has(PanelSections.Build))
            {
                // BUILD — buy reinforcement convoys: a row per convoy (name + contents + cost) with a BUY button.
                UiFactory.SectionHeader(layout.transform, "BUILD — reinforce", theme);
                UiFactory.PreferredHeight(UiFactory.Label("BuildAircraft", layout.transform,
                    "AIRCRAFT — spawn from your airbases (not bought here).", 11f, theme.Muted).gameObject, 16f);
                UiFactory.PreferredHeight(UiFactory.Label("BuildHint", layout.transform,
                    "Spend faction funds on reinforcement convoys (they arrive off-map and drive to the front). Aircraft are flown from your airbases via the game's spawn menu. Every purchase also costs attrition — more so once your bases are lost.",
                    11f, theme.Muted).gameObject, 56f);
                _buildFunds = UiFactory.Label("BuildFunds", layout.transform, "Funds: —", 12f, theme.Accent);
                UiFactory.PreferredHeight(_buildFunds.gameObject, 18f);
                _buildContainer = UiFactory.VerticalLayout("HqBuild", layout.transform, 3f, new RectOffset(0, 0, 0, 0)).transform;
                _buildEmpty = UiFactory.Label("BuildEmpty", layout.transform, "No convoys offered for this faction/map. Aircraft still spawn from your airbases.", 12f, theme.Muted);
                UiFactory.PreferredHeight(_buildEmpty.gameObject, 36f);
                // Order/queue echo so the player sees their purchase reflected HERE (not only in the WAR feed).
                UiFactory.PreferredHeight(UiFactory.Label("BuildQHdr", layout.transform, "ORDERS", 11f, theme.Muted).gameObject, 16f);
                _buildStatus = UiFactory.Label("BuildStatus", layout.transform, "No orders yet. Pick a convoy above to reinforce.", 11f, theme.Text);
                UiFactory.PreferredHeight(_buildStatus.gameObject, 64f);
            }

            if (Has(PanelSections.Scoreboard))
            {
                // SCOREBOARD — the attrition win condition: both factions' score (with a bar), funds, losses.
                UiFactory.SectionHeader(layout.transform, "ATTRITION", theme);
                _scoreTitle = UiFactory.Label("ScoreTitle", layout.transform,
                    "Drive the enemy's score to zero. It falls as a side loses units and bases — and as it spends on reinforcement (faster once bases are lost).",
                    11f, theme.Muted);
                UiFactory.PreferredHeight(_scoreTitle.gameObject, 40f);

                _scoreBlu = UiFactory.Label("ScoreBlu", layout.transform, "BLUFOR", 13f, BluColor);
                UiFactory.PreferredHeight(_scoreBlu.gameObject, 20f);
                _scoreBluBar = MakeBar("BluBar", layout.transform, BluColor);

                _scoreOp = UiFactory.Label("ScoreOp", layout.transform, "OPFOR", 13f, OpColor);
                UiFactory.PreferredHeight(_scoreOp.gameObject, 20f);
                _scoreOpBar = MakeBar("OpBar", layout.transform, OpColor);

                _scoreStatus = UiFactory.Label("ScoreStatus", layout.transform, "", 12f, theme.Text);
                UiFactory.PreferredHeight(_scoreStatus.gameObject, 22f);
            }

            if (Has(PanelSections.Feed))
            {
                // FEED — production status + recent battle events (what the commander is doing).
                UiFactory.SectionHeader(layout.transform, "FEED", theme);
                _hqBody = UiFactory.Label("HqBody", layout.transform, "", 12f, theme.Muted);
                UiFactory.PreferredHeight(_hqBody.gameObject, 110f);
            }
        }

        private static readonly Color OnColor = new Color(0.30f, 0.85f, 0.45f, 1f);

        // A thin progress bar: a dark track with a colored fill child whose right anchor encodes the fraction.
        private static Image MakeBar(string name, Transform parent, Color fill)
        {
            var track = UiFactory.Panel(name + "Track", parent, new Color(0.15f, 0.15f, 0.18f, 1f));
            UiFactory.PreferredHeight(track.gameObject, 12f);
            var bar = UiFactory.Panel(name + "Fill", track, fill);
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.offsetMin = Vector2.zero; bar.offsetMax = Vector2.zero;
            return bar.GetComponent<Image>();
        }

        /// <summary>Render the attrition scoreboard — both factions' score (as a bar), funds, losses, and the
        /// win state. The bar reference is the highest score yet seen, so both bars shrink as the war attrits.</summary>
        public void RenderScoreboard(Cmd.WarfareCampaign.Scoreboard b)
        {
            if (_scoreBlu == null) return;
            _scoreMax = Math.Max(_scoreMax, Math.Max(b.BluforScore, b.OpforScore));
            float denom = Math.Max(1f, _scoreMax);

            _scoreBlu.text = $"{b.BluforName} [{(b.BluforAi ? "AI" : "YOU")}]  {b.BluforScore:0}  ·  ${b.BluforFunds:0}  ·  -{b.BluforUnitsLost}u/-{b.BluforBasesLost}b";
            _scoreOp.text = $"{b.OpforName} [{(b.OpforAi ? "AI" : "YOU")}]  {b.OpforScore:0}  ·  ${b.OpforFunds:0}  ·  -{b.OpforUnitsLost}u/-{b.OpforBasesLost}b";
            SetBar(_scoreBluBar, b.BluforScore / denom);
            SetBar(_scoreOpBar, b.OpforScore / denom);

            if (b.Over)
            {
                _scoreStatus.text = b.WinnerName != null ? $"WAR OVER — {b.WinnerName} WINS" : "WAR OVER — DRAW";
                _scoreStatus.color = OnColor;
            }
            else
            {
                _scoreStatus.text = "War in progress — drive a faction to zero to win.";
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

        /// <summary>The kind the player has armed to drop on the next map click (null = none).</summary>
        public Cmd.ObjectiveKind? ArmedObjective => _armedObjective;
        /// <summary>The objective currently selected for editing (null = none).</summary>
        // A small kind-colored bullet (TMP rich-text) so rows read by kind at a glance — color from the single
        // source of truth (ObjectiveVisuals), so the row dot and the map marker can never drift apart.
        private static string Dot(Cmd.ObjectiveKind kind) => $"<color=#{ObjectiveVisuals.Hex(kind)}>●</color> ";

        public string SelectedObjectiveId => _selectedObjectiveId;
        /// <summary>Set the selected objective (e.g. when the player clicks its map marker).</summary>
        public void SetSelectedObjective(string id) => _selectedObjectiveId = id;
        /// <summary>Clear the armed drop-kind (e.g. after a successful drop).</summary>
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

        /// <summary>Render the objective palette state + the live objective list + the selected-objective editor.
        /// The list rows come from the same operations read-model the map markers use, so panel and map agree.</summary>
        public void RenderObjectives(Cmd.HqSnapshot hq)
        {
            if (_objContainer == null) return;

            // Palette: highlight the armed kind.
            foreach (var kb in _kindButtons)
                kb.Img.color = _armedObjective == kb.Kind ? OnColor : _theme.ButtonIdle;

            if (_objHint != null)
            {
                _objHint.text = _armedObjective.HasValue
                    ? $"Drop {ObjectiveVisuals.Name(_armedObjective.Value)}: click a spot on the map."
                    : "Pick a kind, then click the map. Click a marker to select & edit it.";
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
                    r.Label.color = sel ? OnColor : _theme.Text;
                    r.BtnLabel.text = "SELECT";
                    r.BtnImg.color = sel ? OnColor : _theme.ButtonIdle;   // selected = active green (consistent)
                    r.Go.SetActive(true);
                    _objRows[i] = r;
                }
                else r.Go.SetActive(false);
            }

            if (_objEditor != null)
            {
                if (_selectedObjectiveId == null) { _objEditor.text = "No objective selected."; }
                else
                {
                    string text = "Editing selected objective.";
                    if (ops != null)
                        foreach (var o in ops)
                            if (o.ObjectiveId == _selectedObjectiveId)
                            {
                                // Show the objective + its live state so selecting it explains itself: who's on
                                // it (squad count), what combat phase, and whose objective it is.
                                string owner = o.PlayerOwned ? "yours" : "AI";
                                text = $"{ObjectiveVisuals.Name(o.Kind)} · {ObjectiveVisuals.PhaseLabel(o.Phase)} · {o.SquadCount} squad{(o.SquadCount == 1 ? "" : "s")} · {owner} · Priority {o.Priority:0.#} (PRIO -/+)";
                                break;
                            }
                    _objEditor.text = text;
                }
            }

            RenderAssignList(hq);
        }

        // List the FREE, suitable squads for the selected objective, each with an ASSIGN button, so the player
        // can hand a squad to that objective. (Releasing a squad back to the AI is done from the squad card's
        // AI/YOU autonomy toggle, not here — there is no release action in this list.)
        private void RenderAssignList(Cmd.HqSnapshot hq)
        {
            if (_assignContainer == null) return;
            var ops = hq?.Operations;
            var squads = hq?.Squads;

            // Find the selected objective's kind.
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
            // Free (unassigned) + suitable squads — the ones that can be handed to this objective.
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
                    r.BtnImg.color = OnColor;   // actionable = green (was faction-accent magenta — clashed)
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

            // The two toggles — green when on; reflect live state.
            if (_aiCmdImg != null && hq != null)
            {
                _aiCommanderOn = hq.AiCreatesObjectives;
                _autoFillOn = hq.AiAutoFill;
                _aiCmdImg.color = _aiCommanderOn ? OnColor : _theme.ButtonIdle;
                _autoFillImg.color = _autoFillOn ? OnColor : _theme.ButtonIdle;
                if (_aiCmdLabel != null) _aiCmdLabel.text = _aiCommanderOn ? "AI COMMANDER: ON" : "AI COMMANDER: OFF";
                if (_autoFillLabel != null) _autoFillLabel.text = _autoFillOn ? "AI AUTO-FILL: ON" : "AI AUTO-FILL: OFF";
            }

            // BUILD menu (Build section) — available whenever a catalog exists. Affordability is judged net of
            // already-queued spend so the per-row BUY tint agrees with the "After" over-commit warning below.
            if (_buildContainer != null) RenderBuildRows(catalog, funds, hq?.QueuedCost ?? 0f);
            if (_buildFunds != null)
            {
                float after = funds - hq.QueuedCost;
                _buildFunds.text = $"Funds: {funds:0}  ·  Queued: {hq.QueuedCost:0}  ·  After: {after:0}";
                _buildFunds.color = after < 0f ? new Color(1f, 0.5f, 0.5f) : _theme.Accent;
            }
            if (_buildStatus != null)
            {
                // Echo the production queue + the most recent order so a purchase is visible HERE, not only the feed.
                var sb = new System.Text.StringBuilder();
                if (hq != null)
                {
                    foreach (var line in hq.Production.Take(3)) sb.AppendLine(line);
                    foreach (var e in hq.Recent)
                        if (e.Kind == Cmd.ReportKind.ProductionQueued) { sb.AppendLine("· " + e.Text); break; }
                }
                _buildStatus.text = sb.Length > 0 ? sb.ToString().TrimEnd()
                    : "No orders in progress. Pick a convoy above to reinforce (it arrives off-map and drives in).";
            }

            // OPERATIONS (Operations section).
            if (_opsContainer != null) RenderOpRows(running ? hq.Operations : null);

            // SQUADS (Squads section).
            if (_squadsContainer != null) RenderSquadRows(running ? hq.Squads : null);

            // FEED (Feed section) — production + recent events.
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

        // Squad rows: "Name · Family ×N — activity" + an AUTO/MANUAL toggle. Pooled + index-captured.
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
                    // Composition ("2× MBT, 1× IFV") when known, else family ×strength; show have/need when under target.
                    string comp = !string.IsNullOrEmpty(s.Composition) ? s.Composition : $"{s.Family} ×{s.Strength}";
                    string need = s.TargetStrength > s.Strength ? $" ({s.Strength}/{s.TargetStrength})" : "";
                    r.Label.text = $"{s.Name} · {comp}{need} — {s.Activity}";
                    r.Label.color = s.Depleted ? new Color(1f, 0.5f, 0.5f) : _theme.Text;
                    bool manual = s.Autonomy == Cmd.AutonomyLevel.Manual;
                    r.BtnLabel.text = manual ? "YOU" : "AI";   // who controls this squad (tap to switch)
                    r.BtnImg.color = manual ? _theme.Accent : OnColor;
                    r.Go.SetActive(true);
                    _squadRows[i] = r;
                }
                else r.Go.SetActive(false);
            }
        }

        // Build rows: "name [contents] · cost" + a BUY button (greyed when unaffordable). Pooled.
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
                    r.BtnImg.color = afford ? _theme.Accent : _theme.ButtonIdle;
                    r.Go.SetActive(true);
                    _buildRows[i] = r;
                }
                else r.Go.SetActive(false);
            }
        }

        // Shared row scaffold for the pooled list builders (DRY): a fixed-height HorizontalLayout with a
        // flexible label + a fixed-width action button. Each caller wires the button's onClick to its own id
        // source and stores it in its own pool struct — the construction boilerplate lives here once.
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

        // Build/grow a pool of generic label+button rows in a container; button calls onClick(row.Id).
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

        public void SetVisible(bool visible)
        {
            if (_root != null) _root.gameObject.SetActive(visible);
        }

        // Interactive per-operation rows in the HQ section: label + an AUTO/MANUAL toggle that takes that one
        // operation off the AI (or hands it back). Pooled + index-captured like the order rows.
        private void RenderOpRows(IReadOnlyList<Nucleus.Core.Command.OperationView> ops)
        {
            int count = ops?.Count ?? 0;
            if (_opsEmpty != null) _opsEmpty.gameObject.SetActive(count == 0);
            EnsureOpRows(System.Math.Min(count, 5)); // cap visible op rows
            for (int i = 0; i < _opRows.Count; i++)
            {
                if (ops != null && i < count && i < 5)
                {
                    var op = ops[i];
                    var r = _opRows[i];
                    r.OpId = op.Id;
                    r.Label.text = $"{Dot(op.Kind)}{ObjectiveVisuals.Name(op.Kind)} — {ObjectiveVisuals.PhaseLabel(op.Phase)} [{op.Status}]";
                    bool manual = op.Autonomy == Nucleus.Core.Command.AutonomyLevel.Manual;
                    r.BtnLabel.text = manual ? "YOU" : "AI";   // who runs this operation (tap to switch)
                    r.BtnImg.color = manual ? _theme.Accent : OnColor;
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
