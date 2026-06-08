using System.Collections.Generic;
using Nucleus.Core.Model;
using Nucleus.Core.Ports;
using Cmd = Nucleus.Core.Command;
using UnityEngine;
using UnityEngine.UI;

namespace Nucleus.Ui
{
    /// <summary>Map overlay on the icon layer: one selectable parent marker per ORDER (colored by goal kind).
    /// Selecting an order (its <see cref="Cmd.OrderView.GoalObjectiveId"/> == the shared
    /// <see cref="Nucleus.Composition.CommanderRuntime.SelectedObjectiveId"/>, or one of its child nodes)
    /// expands its prerequisite child-node markers with connector lines and dim/unreachable styling, so the map
    /// "drills into" the order tree. In-flight production shows as dashed arrival markers at the home front.
    /// Reads the SAME <see cref="Cmd.OrderView"/> data as the canopy <c>WorldMarkerLayer</c>, so the two agree.
    /// Pooled. Click routing + selection writes live in the runtime (so drag-to-move still works).</summary>
    public sealed class MapOverlay
    {
        /// <summary>Max parent orders drawn — shared with the canopy layer so map and HUD never disagree on the
        /// objective set. Children are drawn in addition for the expanded order only.</summary>
        public const int MaxOrderMarkers = 6;
        private const int MaxArrivalMarkers = 4;

        private readonly Transform _layer;
        private readonly IMapProjection _projection;
        private readonly Theme _theme;
        private readonly List<Image> _markers = new List<Image>();
        private readonly List<TMPro.TextMeshProUGUI> _objLabels = new List<TMPro.TextMeshProUGUI>();
        private readonly List<Image> _labelBgs = new List<Image>();   // contrast pill behind each map label
        private readonly List<Image> _lines = new List<Image>();      // parent -> child connectors
        private readonly List<Image> _arrivalRings = new List<Image>();
        private readonly List<TMPro.TextMeshProUGUI> _arrivalLabels = new List<TMPro.TextMeshProUGUI>();
        private TMPro.TextMeshProUGUI _selInfo;   // "why/what" header for the selected order/node
        private Image _selRing;                   // ring drawn around the selected marker

        public MapOverlay(Transform iconLayer, IMapProjection projection, Theme theme)
        {
            _projection = projection;
            _theme = theme ?? Theme.Default;
            // First sibling of the icon layer, so the game's own unit icons render ON TOP of our markers.
            var go = new GameObject("CommanderOverlay", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(iconLayer, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.localPosition = Vector3.zero;
            rt.localScale = Vector3.one;
            rt.SetAsFirstSibling();
            _layer = rt;
        }

        /// <summary>Draw a selectable marker per order (parent = the goal), and for the selected order its child
        /// prerequisite markers + connector lines. <paramref name="selectedId"/> is the shared
        /// <see cref="Nucleus.Composition.CommanderRuntime.SelectedObjectiveId"/> (bidirectional with the panel) —
        /// an order is expanded when it is its goal OR one of its child nodes. <paramref name="unitPositions"/> +
        /// <paramref name="queue"/> are optional: when a production queue view is supplied, in-flight buys show as
        /// dashed arrival markers at the friendly centroid (home front).</summary>
        public void RenderObjectives(IReadOnlyList<Cmd.OrderView> orders, string selectedId,
            IReadOnlyDictionary<string, Vec3> unitPositions = null,
            IReadOnlyList<Cmd.QueueItemView> queue = null)
        {
            int mi = 0, li = 0, shown = 0;
            Vec3 selLocal = default; bool haveSel = false, selIsNode = false;
            Cmd.OrderView selOrder = default; Cmd.OrderNodeView selNode = default;

            if (orders != null)
            {
                foreach (var ord in orders)
                {
                    if (ord.Status != Cmd.OrderStatus.Active) continue;
                    if (shown >= MaxOrderMarkers) break;
                    shown++;

                    // Anchor the parent at the GOAL NODE's live position (== OrderView.Position until the player
                    // drags it; Order.Position is fixed, but MoveObjective updates the goal objective), so the
                    // marker tracks a drag-to-move.
                    var pLocal = _projection.WorldToMapLocal(GoalPosition(ord));
                    bool pSel = ord.GoalObjectiveId == selectedId;
                    bool expanded = pSel || HasSelectedNode(ord, selectedId);

                    var pColor = ObjectiveVisuals.Color(ord.GoalKind);
                    DrawMarker(mi, pLocal, pColor, UiTokens.MarkerSize);
                    DrawLabel(mi, pLocal, ObjectiveVisuals.Name(ord.GoalKind), pSel ? _theme.Active : pColor, pSel);
                    mi++;
                    if (pSel) { selLocal = pLocal; haveSel = true; selIsNode = false; selOrder = ord; }

                    if (!expanded || ord.Nodes == null) continue;
                    foreach (var n in ord.Nodes)
                    {
                        if (n.IsGoal) continue;   // the goal is the parent marker; children are its prerequisites
                        var cLocal = _projection.WorldToMapLocal(n.Position);
                        var connector = n.Unreachable ? _theme.WarnText : _theme.Muted;
                        DrawLine(Line(li++), pLocal, cLocal, connector);

                        bool cSel = n.ObjectiveId == selectedId;
                        bool dim = n.Complete || !n.Active;            // done / not-yet-fielded reads dimmer
                        var cColor = n.Unreachable ? _theme.WarnText : ObjectiveVisuals.Color(n.Kind);
                        if (dim) cColor.a = UiTokens.LineOpacity;
                        DrawMarker(mi, cLocal, cColor, UiTokens.MarkerSizeFallback);
                        DrawLabel(mi, cLocal, ObjectiveVisuals.Name(n.Kind), cSel ? _theme.Active : cColor, cSel);
                        mi++;
                        if (cSel) { selLocal = cLocal; haveSel = true; selIsNode = true; selOrder = ord; selNode = n; }
                    }
                }
            }
            for (int i = mi; i < _markers.Count; i++) _markers[i].gameObject.SetActive(false);
            for (int i = mi; i < _objLabels.Count; i++) _objLabels[i].gameObject.SetActive(false);
            for (int i = mi; i < _labelBgs.Count; i++) _labelBgs[i].gameObject.SetActive(false);
            for (int i = li; i < _lines.Count; i++) _lines[i].gameObject.SetActive(false);

            RenderArrivals(unitPositions, queue);

            // The "why/what" header next to the selection: intent + phase/status + force + ownership.
            EnsureSelInfo();
            if (haveSel)
            {
                var irt = (RectTransform)_selInfo.transform;
                // Edge-clamp: flip which side of the marker the header grows toward so it never clips off the
                // map. Lower half → grow upward (pivot bottom); right half → grow leftward (pivot right).
                bool low = selLocal.Y < 0f, right = selLocal.X > 0f;
                irt.pivot = new Vector2(right ? 1f : 0f, low ? 0f : 1f);
                _selInfo.alignment = right ? TMPro.TextAlignmentOptions.TopRight : TMPro.TextAlignmentOptions.TopLeft;
                irt.localPosition = new Vector3(selLocal.X + (right ? -12f : 12f), selLocal.Y + (low ? 12f : 20f), 0f);
                _selInfo.text = selIsNode ? NodeInfo(selNode) : OrderInfo(selOrder);
                _selInfo.color = (selIsNode && selNode.Unreachable) ? _theme.WarnText : _theme.Active;
                _selInfo.gameObject.SetActive(true);
            }
            else _selInfo.gameObject.SetActive(false);

            EnsureSelRing();
            if (haveSel)
            {
                var rt = (RectTransform)_selRing.transform;
                rt.localPosition = new Vector3(selLocal.X, selLocal.Y, 0f);
                rt.sizeDelta = new Vector2(UiTokens.SelectionRingSize, UiTokens.SelectionRingSize);
                _selRing.gameObject.SetActive(true);
            }
            else _selRing.gameObject.SetActive(false);
        }

        // Reinforcements carry no map position of their own, so anchor in-flight buys at the friendly centroid
        // (the home front) and stack them with their ETA. No-op until a queue view + unit positions are supplied.
        private void RenderArrivals(IReadOnlyDictionary<string, Vec3> unitPositions, IReadOnlyList<Cmd.QueueItemView> queue)
        {
            int ar = 0;
            if (queue != null && queue.Count > 0 && unitPositions != null && unitPositions.Count > 0)
            {
                var cl = _projection.WorldToMapLocal(Centroid(unitPositions));
                foreach (var q in queue)
                {
                    if (ar >= MaxArrivalMarkers) break;
                    float y = cl.Y + ar * 16f;
                    var col = _theme.EnRoute;

                    var ring = ArrivalRing(ar);
                    var rrt = (RectTransform)ring.transform;
                    rrt.localPosition = new Vector3(cl.X, y, 0f);
                    rrt.sizeDelta = new Vector2(UiTokens.MarkerSizeFallback, UiTokens.MarkerSizeFallback);
                    ring.color = col;

                    var lbl = ArrivalLabel(ar);
                    ((RectTransform)lbl.transform).localPosition = new Vector3(cl.X + UiTokens.MapLabelOffsetX, y, 0f);
                    string what = !string.IsNullOrEmpty(q.Contents) ? q.Contents : q.Name;
                    lbl.text = q.EtaSeconds > 0f ? $"{what} · {q.EtaSeconds:0}s" : what;
                    lbl.color = col;
                    ar++;
                }
            }
            for (int i = ar; i < _arrivalRings.Count; i++) _arrivalRings[i].gameObject.SetActive(false);
            for (int i = ar; i < _arrivalLabels.Count; i++) _arrivalLabels[i].gameObject.SetActive(false);
        }

        private static bool HasSelectedNode(Cmd.OrderView ord, string selectedId)
        {
            if (selectedId == null || ord.Nodes == null) return false;
            foreach (var n in ord.Nodes) if (!n.IsGoal && n.ObjectiveId == selectedId) return true;
            return false;
        }

        /// <summary>The goal node's live position (tracks drag-to-move); falls back to the order's fixed
        /// <see cref="Cmd.OrderView.Position"/>. Shared with the canopy layer so both anchor a parent identically.</summary>
        public static Vec3 GoalPosition(Cmd.OrderView ord)
        {
            if (ord.Nodes != null)
                foreach (var n in ord.Nodes) if (n.IsGoal) return n.Position;
            return ord.Position;
        }

        private static Vec3 Centroid(IReadOnlyDictionary<string, Vec3> positions)
        {
            float x = 0f, y = 0f, z = 0f; int n = 0;
            foreach (var p in positions.Values) { x += p.X; y += p.Y; z += p.Z; n++; }
            return n > 0 ? new Vec3(x / n, y / n, z / n) : default;
        }

        // Selection detail for a picked prerequisite node — mirrors PresentationBuilder.BuildNodeDetail wording.
        private string NodeInfo(Cmd.OrderNodeView n)
        {
            string status = n.Complete ? "Complete"
                : n.Active ? $"Active · {ObjectiveVisuals.PhaseLabel(n.Phase)}"
                : n.DependenciesMet ? "Ready — awaiting force"
                : "Blocked — waiting on prerequisites";
            string force = n.SquadCount > 0 ? $"{n.SquadCount} squad{(n.SquadCount == 1 ? "" : "s")}" : "no force yet";
            string who = n.Autonomy == Cmd.AutonomyLevel.Manual ? "yours" : "AI";
            string reach = n.Unreachable ? "\nNo force can reach this" : "";
            return $"{ObjectiveVisuals.Name(n.Kind)}\n{status}\n{force} · {who}{reach}";
        }

        // Selection detail for a picked order (the goal): status + prerequisite progress + ownership.
        private string OrderInfo(Cmd.OrderView o)
        {
            int done = 0, total = 0;
            if (o.Nodes != null)
                foreach (var n in o.Nodes) { if (n.IsGoal) continue; total++; if (n.Complete) done++; }
            string who = (o.PlayerOwned || o.Autonomy == Cmd.AutonomyLevel.Manual) ? "yours" : "AI";
            string prog = total > 0 ? $"{done}/{total} prereqs done" : "no prerequisites";
            return $"{ObjectiveVisuals.Name(o.GoalKind)}\n{OrderStatusLabel(o.Status)}\n{prog}\n{who}";
        }

        private static string OrderStatusLabel(Cmd.OrderStatus s)
            => s == Cmd.OrderStatus.Complete ? "complete"
             : s == Cmd.OrderStatus.Failed ? "failed" : "active";

        private void DrawMarker(int i, Vec3 local, Color color, float size)
        {
            var m = Marker(i);
            var rt = (RectTransform)m.transform;
            rt.localPosition = new Vector3(local.X, local.Y, 0f);
            rt.sizeDelta = new Vector2(size, size);
            m.color = color;
        }

        // Pooled map label drawn next to its marker, over a contrast pill so it reads on any terrain.
        private void DrawLabel(int i, Vec3 local, string text, Color color, bool selected)
        {
            var lbl = Label(i);
            ((RectTransform)lbl.transform).localPosition = new Vector3(local.X + UiTokens.MapLabelOffsetX, local.Y, 0f);
            lbl.text = text;
            lbl.color = color;
            lbl.fontSize = selected ? UiTokens.FontHeader : UiTokens.FontHint;   // selection cue
            var bg = LabelBg(i);
            var brt = (RectTransform)bg.transform;
            brt.localPosition = new Vector3(local.X + UiTokens.MapLabelOffsetX - 3f, local.Y, 0f);
            brt.sizeDelta = new Vector2(lbl.preferredWidth + 6f, lbl.fontSize + 4f);
            brt.SetSiblingIndex(lbl.transform.GetSiblingIndex()); // sit just behind its label
        }

        // Pooled map label, drawn next to its marker.
        private TMPro.TextMeshProUGUI Label(int i)
        {
            while (_objLabels.Count <= i)
            {
                var t = UiFactory.Label("ObjLabel" + _objLabels.Count, _layer, "", UiTokens.FontHint, Color.white);
                var rt = t.rectTransform;
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(120f, 16f);
                t.alignment = TMPro.TextAlignmentOptions.Left;
                _objLabels.Add(t);
            }
            _objLabels[i].gameObject.SetActive(true);
            return _objLabels[i];
        }

        // Pooled dark pill drawn behind a map label so the text reads over bright/varied terrain.
        private Image LabelBg(int i)
        {
            while (_labelBgs.Count <= i)
            {
                var img = UiFactory.LineImage("ObjLabelBg" + _labelBgs.Count, _layer, _theme.LabelBackdrop);
                var rt = (RectTransform)img.transform;
                rt.pivot = new Vector2(0f, 0.5f);
                img.raycastTarget = false;
                _labelBgs.Add(img);
            }
            _labelBgs[i].gameObject.SetActive(true);
            return _labelBgs[i];
        }

        private Image ArrivalRing(int i)
        {
            while (_arrivalRings.Count <= i)
            {
                var img = UiFactory.Ring("ArrivalRing" + _arrivalRings.Count, _layer, _theme.EnRoute, dashed: true);
                ((RectTransform)img.transform).pivot = new Vector2(0.5f, 0.5f);
                _arrivalRings.Add(img);
            }
            _arrivalRings[i].gameObject.SetActive(true);
            return _arrivalRings[i];
        }

        private TMPro.TextMeshProUGUI ArrivalLabel(int i)
        {
            while (_arrivalLabels.Count <= i)
            {
                var t = UiFactory.Label("ArrivalLabel" + _arrivalLabels.Count, _layer, "", UiTokens.FontHint, _theme.EnRoute);
                var rt = t.rectTransform;
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(160f, 16f);
                t.alignment = TMPro.TextAlignmentOptions.Left;
                _arrivalLabels.Add(t);
            }
            _arrivalLabels[i].gameObject.SetActive(true);
            return _arrivalLabels[i];
        }

        private void EnsureSelRing()
        {
            if (_selRing != null) return;
            _selRing = UiFactory.Ring("ObjSelRing", _layer, _theme.Active);
            var rt = (RectTransform)_selRing.transform;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private void EnsureSelInfo()
        {
            if (_selInfo != null) return;
            _selInfo = UiFactory.Label("ObjSelInfo", _layer, "", UiTokens.FontHint, _theme.Active);
            var rt = _selInfo.rectTransform;
            rt.pivot = new Vector2(0f, 1f);                 // top-left anchored to the marker
            rt.sizeDelta = new Vector2(150f, 64f);
            _selInfo.alignment = TMPro.TextAlignmentOptions.TopLeft;
        }

        /// <summary>Hide all overlay graphics (e.g. when the map closes).</summary>
        public void Clear()
        {
            foreach (var m in _markers) m.gameObject.SetActive(false);
            foreach (var l in _objLabels) l.gameObject.SetActive(false);
            foreach (var l in _labelBgs) l.gameObject.SetActive(false);
            foreach (var l in _lines) l.gameObject.SetActive(false);
            foreach (var r in _arrivalRings) r.gameObject.SetActive(false);
            foreach (var l in _arrivalLabels) l.gameObject.SetActive(false);
            if (_selRing != null) _selRing.gameObject.SetActive(false);
            if (_selInfo != null) _selInfo.gameObject.SetActive(false);
        }

        private Image Marker(int i)
        {
            while (_markers.Count <= i)
            {
                var img = UiFactory.LineImage("CmdMarker" + _markers.Count, _layer, Color.white);
                var rt = (RectTransform)img.transform;
                rt.pivot = new Vector2(0.5f, 0.5f);
                // The game's own target sprite, tinted by the kind color, so the marker reads as native.
                if (NativeIcons.Warhead != null)
                {
                    img.sprite = NativeIcons.Warhead;
                    img.preserveAspect = true;
                    rt.sizeDelta = new Vector2(UiTokens.MarkerSize, UiTokens.MarkerSize);
                }
                else rt.sizeDelta = new Vector2(UiTokens.MarkerSizeFallback, UiTokens.MarkerSizeFallback);
                _markers.Add(img);
            }
            _markers[i].gameObject.SetActive(true);
            return _markers[i];
        }

        private Image Line(int i)
        {
            while (_lines.Count <= i) _lines.Add(UiFactory.LineImage("CmdLine" + _lines.Count, _layer, Color.white));
            _lines[i].gameObject.SetActive(true);
            return _lines[i];
        }

        private static void DrawLine(Image line, Vec3 a, Vec3 b, Color color)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            var rt = (RectTransform)line.transform;
            rt.localPosition = new Vector3(a.X, a.Y, 0f);
            rt.localRotation = Quaternion.Euler(0f, 0f, ang);
            rt.sizeDelta = new Vector2(len, 2f);
            color.a = UiTokens.LineOpacity;
            line.color = color;
        }
    }
}
