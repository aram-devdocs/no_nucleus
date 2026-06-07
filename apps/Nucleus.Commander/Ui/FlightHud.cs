using System.Collections.Generic;
using Nucleus.Ui;
using Cmd = Nucleus.Core.Command;
using UnityEngine;
using UnityEngine.UI;

namespace Nucleus.Composition
{
    /// <summary>
    /// A compact, always-on objective HUD shown in the bottom-right WHILE FLYING (map closed) — the fix for
    /// "no HUD when in game flying". Lists the active operations (kind · phase · squads · priority), highlights
    /// the top-priority one, and shows the AI's latest intent line. Lives on a screen-space overlay canvas of
    /// its own (the map overlay is hidden when the map closes). Pooled labels, no per-frame allocations,
    /// raycastTarget off so it never eats cockpit input. Fed from the already-throttled Hq snapshot.
    /// </summary>
    public sealed class FlightHud
    {
        private const int MaxRows = 5;

        private readonly RectTransform _root;
        private readonly TMPro.TextMeshProUGUI _header;
        private readonly TMPro.TextMeshProUGUI _intent;
        private readonly List<TMPro.TextMeshProUGUI> _rows = new List<TMPro.TextMeshProUGUI>();
        private readonly RectTransform _rowsParent;

        // Reused across renders so sorting allocates nothing on the hot path.
        private readonly List<Cmd.OperationView> _buf = new List<Cmd.OperationView>();
        private static readonly System.Comparison<Cmd.OperationView> ByPriorityDesc =
            (a, b) => b.Priority.CompareTo(a.Priority);

        public FlightHud(Transform canvas)
        {
            // Higher opacity so the HUD stays legible over bright sky/terrain (was washing out at 0.72).
            _root = UiFactory.Panel("NucleusFlightHud", canvas, new Color(0.04f, 0.06f, 0.08f, 0.88f));
            _root.anchorMin = _root.anchorMax = _root.pivot = new Vector2(1f, 0f); // bottom-right
            _root.sizeDelta = new Vector2(360f, 168f);
            _root.anchoredPosition = new Vector2(-18f, 18f);
            _root.gameObject.GetComponent<Image>().raycastTarget = false;
            _root.SetAsLastSibling();

            var col = UiFactory.VerticalLayout("Col", _root, 2f, new RectOffset(10, 10, 8, 8));
            UiFactory.Stretch((RectTransform)col.transform);

            _header = UiFactory.Label("HudHeader", col.transform, "NUCLEUS", 15f, NativeColors.Friendly);
            UiFactory.PreferredHeight(_header.gameObject, 20f);
            _intent = UiFactory.Label("HudIntent", col.transform, "", 12f, new Color(0.8f, 0.85f, 0.9f, 1f));
            UiFactory.PreferredHeight(_intent.gameObject, 18f);

            var rows = UiFactory.VerticalLayout("Rows", col.transform, 1f, new RectOffset(0, 0, 2, 0));
            _rowsParent = (RectTransform)rows.transform;
        }

        public void SetVisible(bool on)
        {
            if (_root != null && _root.gameObject.activeSelf != on) _root.gameObject.SetActive(on);
        }

        public void Render(Cmd.HqSnapshot hq)
        {
            SetVisible(true);
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
                row.text = (top ? "▶ " : "  ") +
                    $"{KindTag(op.Kind)}  {op.Phase}  {op.SquadCount}sq  P{op.Priority:0.#}";
                var c = ObjectiveColor(op.Kind);
                row.color = top ? Color.Lerp(c, Color.white, 0.35f) : c;
                row.fontSize = top ? 13f : 12f;
            }
            for (int i = n; i < _rows.Count; i++) _rows[i].gameObject.SetActive(false);
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

        private static string KindTag(Cmd.ObjectiveKind kind)
        {
            switch (kind)
            {
                case Cmd.ObjectiveKind.CapturePoint: return "CAPTURE";
                case Cmd.ObjectiveKind.DestroyTarget: return "DESTROY";
                case Cmd.ObjectiveKind.DefendArea: return "DEFEND";
                case Cmd.ObjectiveKind.ControlAirspace: return "AIR";
                case Cmd.ObjectiveKind.Resupply: return "SUPPLY";
                default: return "RECON";
            }
        }

        private static Color ObjectiveColor(Cmd.ObjectiveKind kind)
        {
            switch (kind)
            {
                case Cmd.ObjectiveKind.CapturePoint: return new Color(0.4f, 0.8f, 1f);
                case Cmd.ObjectiveKind.DestroyTarget: return new Color(1f, 0.45f, 0.4f);
                case Cmd.ObjectiveKind.DefendArea: return new Color(0.45f, 0.9f, 0.55f);
                case Cmd.ObjectiveKind.ControlAirspace: return new Color(0.7f, 0.6f, 1f);
                case Cmd.ObjectiveKind.Resupply: return new Color(1f, 0.85f, 0.4f);
                default: return new Color(0.85f, 0.85f, 0.85f);
            }
        }
    }
}
