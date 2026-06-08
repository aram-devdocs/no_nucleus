using System.Collections.Generic;
using Nucleus.Ui;
using Cmd = Nucleus.Core.Command;
using UnityEngine;
using UnityEngine.UI;

namespace Nucleus.Composition
{
    /// <summary>
    /// The fly-and-command HUD shown WHILE FLYING (map closed) — the fix for "no HUD when in game flying". Two
    /// pieces: a TOP-CENTER war strip (both factions' attrition bars + numbers, plus the single "what to do now"
    /// line) so the war state and the next action read at a glance, and a RIGHT-SIDE operations list (kind ·
    /// phase · squads, top one highlighted) anchored clear of the native aircraft-health HUD. Lives on a
    /// screen-space overlay canvas of its own (the map overlay is hidden when the map closes). Pooled labels, no
    /// per-frame allocations, raycastTarget off so it never eats cockpit input. Fed from the throttled Hq
    /// snapshot; the war strip's bars come from the optional scoreboard (hidden until one is supplied).
    /// </summary>
    public sealed class FlightHud
    {
        private const int MaxRows = 5;

        private readonly RectTransform _root;
        private readonly TMPro.TextMeshProUGUI _header;
        private readonly TMPro.TextMeshProUGUI _intent;
        private readonly List<TMPro.TextMeshProUGUI> _rows = new List<TMPro.TextMeshProUGUI>();
        private readonly RectTransform _rowsParent;
        private readonly Theme _theme;

        // Top-center war strip: the one "what to do" line + both factions' attrition bars/numbers.
        private readonly RectTransform _strip;
        private readonly TMPro.TextMeshProUGUI _stripOrder;
        private readonly RectTransform _scoreGroup;     // bars sub-section — hidden until a scoreboard is supplied
        private readonly TMPro.TextMeshProUGUI _bluLine;
        private readonly TMPro.TextMeshProUGUI _opLine;
        private readonly Image _bluBar;
        private readonly Image _opBar;

        // Reused across renders so sorting allocates nothing on the hot path.
        private readonly List<Cmd.OperationView> _buf = new List<Cmd.OperationView>();
        private static readonly System.Comparison<Cmd.OperationView> ByPriorityDesc =
            (a, b) => b.Priority.CompareTo(a.Priority);

        public FlightHud(Transform canvas, Theme theme)
        {
            _theme = theme ?? Theme.Default;
            _root = UiFactory.Panel("NucleusFlightHud", canvas, _theme.HudBackground);
            // Right edge, vertically centered — clear of the native aircraft-health HUD (bottom) and the war strip (top).
            _root.anchorMin = _root.anchorMax = _root.pivot = new Vector2(1f, 0.5f);
            _root.sizeDelta = new Vector2(UiTokens.HudWidth, UiTokens.HudHeight);
            _root.anchoredPosition = new Vector2(-18f, 0f);
            _root.gameObject.GetComponent<Image>().raycastTarget = false;
            _root.SetAsLastSibling();

            var col = UiFactory.VerticalLayout("Col", _root, 2f, new RectOffset(10, 10, 8, 8));
            UiFactory.Stretch((RectTransform)col.transform);

            _header = UiFactory.Label("HudHeader", col.transform, "NUCLEUS", 15f, NativeColors.Friendly);
            UiFactory.PreferredHeight(_header.gameObject, 20f);
            UiFactory.Divider(col.transform, NativeColors.Friendly); // accent rule so the HUD reads as intentional
            _intent = UiFactory.Label("HudIntent", col.transform, "", 12f, _theme.HudText);
            UiFactory.PreferredHeight(_intent.gameObject, 18f);

            var rows = UiFactory.VerticalLayout("Rows", col.transform, 1f, new RectOffset(0, 0, 2, 0));
            _rowsParent = (RectTransform)rows.transform;

            // --- Top-center war strip ---
            _strip = UiFactory.Panel("NucleusWarStrip", canvas, _theme.HudBackground);
            _strip.anchorMin = _strip.anchorMax = _strip.pivot = new Vector2(0.5f, 1f); // top-center
            _strip.sizeDelta = new Vector2(UiTokens.WarStripWidth, UiTokens.WarStripHeight);
            _strip.anchoredPosition = new Vector2(0f, -8f);
            _strip.gameObject.GetComponent<Image>().raycastTarget = false;
            _strip.SetAsLastSibling();

            var scol = UiFactory.VerticalLayout("WarCol", _strip, 2f, new RectOffset(12, 12, 6, 6));
            UiFactory.Stretch((RectTransform)scol.transform);

            _stripOrder = UiFactory.Label("WarOrder", scol.transform, "", 13f, _theme.HudText,
                TMPro.TextAlignmentOptions.Center);
            _stripOrder.enableWordWrapping = false;
            UiFactory.PreferredHeight(_stripOrder.gameObject, 18f);
            UiFactory.Divider(scol.transform, NativeColors.Friendly); // mod accent rule — reads as intentional

            var scores = UiFactory.VerticalLayout("WarScores", scol.transform, 1f, new RectOffset(0, 0, 0, 0));
            _scoreGroup = (RectTransform)scores.transform;
            _bluLine = UiFactory.Label("WarBlu", scores.transform, "", 11f, _theme.ScoreBlufor);
            _bluLine.enableWordWrapping = false;
            UiFactory.PreferredHeight(_bluLine.gameObject, 14f);
            _bluBar = Bar("WarBluBar", scores.transform, _theme.ScoreBlufor);
            _opLine = UiFactory.Label("WarOp", scores.transform, "", 11f, _theme.ScoreOpfor);
            _opLine.enableWordWrapping = false;
            UiFactory.PreferredHeight(_opLine.gameObject, 14f);
            _opBar = Bar("WarOpBar", scores.transform, _theme.ScoreOpfor);
            _scoreGroup.gameObject.SetActive(false); // shown only once a scoreboard is supplied
        }

        public void SetVisible(bool on)
        {
            if (_root != null && _root.gameObject.activeSelf != on) _root.gameObject.SetActive(on);
            if (_strip != null && _strip.gameObject.activeSelf != on) _strip.gameObject.SetActive(on);
        }

        // A thin attrition bar: a dark track with a colored fill child whose right anchor encodes the fraction
        // (same idiom as CommanderPanel's scoreboard bars, so the two never drift).
        private Image Bar(string name, Transform parent, Color fill)
        {
            var track = UiFactory.Panel(name + "Track", parent, _theme.BarTrack);
            track.gameObject.GetComponent<Image>().raycastTarget = false;
            UiFactory.PreferredHeight(track.gameObject, UiTokens.WarBarHeight);
            var bar = UiFactory.Panel(name + "Fill", track, fill);
            bar.gameObject.GetComponent<Image>().raycastTarget = false;
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.offsetMin = Vector2.zero; bar.offsetMax = Vector2.zero;
            return bar.GetComponent<Image>();
        }

        private static void SetBar(Image bar, float fraction)
        {
            if (bar == null) return;
            float f = Mathf.Clamp01(fraction);
            var rt = bar.rectTransform;
            rt.anchorMax = new Vector2(f, 1f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        public void Render(Cmd.HqSnapshot hq, Cmd.WarfareCampaign.Scoreboard? board = null)
        {
            SetVisible(true);

            // Top-center strip: the single "what to do now" line (always), plus both factions' attrition bars +
            // numbers when a scoreboard is available (Commander-only flight has none — the strip then shows just
            // the guidance line).
            _stripOrder.text = Nucleus.Presentation.PresentationBuilder.Guidance(hq);
            if (board.HasValue)
            {
                var sb = Nucleus.Presentation.PresentationBuilder.BuildScoreboard(board.Value);
                _bluLine.text = sb.BluforLine;
                _opLine.text = sb.OpforLine;
                SetBar(_bluBar, sb.BluforFraction);
                SetBar(_opBar, sb.OpforFraction);
                if (!_scoreGroup.gameObject.activeSelf) _scoreGroup.gameObject.SetActive(true);
            }
            else if (_scoreGroup.gameObject.activeSelf) _scoreGroup.gameObject.SetActive(false);

            _buf.Clear();
            if (hq?.Operations != null)
                foreach (var op in hq.Operations)
                {
                    if (op.Status == Cmd.OperationStatus.Complete || op.Status == Cmd.OperationStatus.Failed) continue;
                    _buf.Add(op);
                }
            _buf.Sort(ByPriorityDesc);

            _header.text = $"NUCLEUS — {_buf.Count} op{(_buf.Count == 1 ? "" : "s")}";

            // AI intent = the newest battle-feed line (Recent is newest-first).
            string intent = "";
            if (hq?.Recent != null)
                foreach (var e in hq.Recent) { intent = e.Text; break; }
            _intent.text = intent;

            int n = _buf.Count < MaxRows ? _buf.Count : MaxRows;
            for (int i = 0; i < n; i++)
            {
                var op = _buf[i];
                var row = Row(i);
                bool top = i == 0;
                // Readiness of the op's squads (Eng/Rte/Frm/Dep/Rsv) so a glance says "forming up" vs "in contact".
                string ready = Readiness(hq, op.Id);
                row.text = (top ? "▶ " : "   ") +
                    $"{Nucleus.Ui.ObjectiveVisuals.Tag(op.Kind)}  {Nucleus.Ui.ObjectiveVisuals.PhaseLabel(op.Phase)}  {op.SquadCount}sq{ready}";
                // Top objective is the one strong visual: the on/selected green + a clear size jump.
                row.color = top ? _theme.Active : Nucleus.Ui.ObjectiveVisuals.Color(op.Kind);
                row.fontSize = top ? 14f : 11f;
            }
            for (int i = n; i < _rows.Count; i++) _rows[i].gameObject.SetActive(false);
        }

        // The dominant readiness of the squads on an operation, as a short suffix ("  Eng"). Empty if none.
        private static string Readiness(Cmd.HqSnapshot hq, string opId)
        {
            if (hq?.Squads == null) return "";
            var best = (Cmd.SquadStatus)(-1);
            foreach (var s in hq.Squads)
            {
                if (s.AssignedOperationId != opId) continue;
                if ((int)best < 0 || Rank(s.Status) > Rank(best)) best = s.Status;
            }
            if ((int)best < 0) return "";
            switch (best)
            {
                case Cmd.SquadStatus.Engaged: return "  Eng";
                case Cmd.SquadStatus.Forming: return "  Frm";
                case Cmd.SquadStatus.Depleted: return "  Dep";
                case Cmd.SquadStatus.Ready: return "  Rte";
                default: return "  Rsv";
            }
        }

        // Attention priority: in-contact first, then forming, hurt, en route, reserve.
        private static int Rank(Cmd.SquadStatus s)
        {
            switch (s)
            {
                case Cmd.SquadStatus.Engaged: return 4;
                case Cmd.SquadStatus.Forming: return 3;
                case Cmd.SquadStatus.Depleted: return 2;
                case Cmd.SquadStatus.Ready: return 1;
                default: return 0;
            }
        }

        private TMPro.TextMeshProUGUI Row(int i)
        {
            while (_rows.Count <= i)
            {
                var t = UiFactory.Label("HudRow" + _rows.Count, _rowsParent, "", 12f, Color.white);
                t.enableWordWrapping = false;
                UiFactory.PreferredHeight(t.gameObject, 18f);
                _rows.Add(t);
            }
            _rows[i].gameObject.SetActive(true);
            return _rows[i];
        }

    }
}
