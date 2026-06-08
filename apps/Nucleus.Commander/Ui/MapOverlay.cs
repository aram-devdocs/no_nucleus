using System.Collections.Generic;
using Nucleus.Core.Model;
using Nucleus.Core.Ports;
using UnityEngine;
using UnityEngine.UI;

namespace Nucleus.Ui
{
    /// <summary>Map overlay on the icon layer: a selectable labelled marker per objective (colored by kind),
    /// status-colored lines to assigned squads, and a "why/what" header for the selection. Pooled.</summary>
    public sealed class MapOverlay
    {
        private readonly Transform _layer;
        private readonly IMapProjection _projection;
        private readonly Theme _theme;
        private readonly List<Image> _markers = new List<Image>();
        private readonly List<TMPro.TextMeshProUGUI> _objLabels = new List<TMPro.TextMeshProUGUI>();
        private readonly List<Image> _labelBgs = new List<Image>();   // contrast pill behind each map label
        private readonly List<TMPro.TextMeshProUGUI> _squadLabels = new List<TMPro.TextMeshProUGUI>();
        private TMPro.TextMeshProUGUI _selInfo;   // "why/what" header for the selected objective
        private readonly List<Image> _lines = new List<Image>();
        private Image _selRing;        // ring drawn around the selected objective marker

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

        /// <summary>Draw a selectable marker per objective (colored by kind), ring on the selected one. Reads the
        /// same operations read-model the panel renders, so map and panel always agree.</summary>
        public void RenderObjectives(IReadOnlyList<Nucleus.Core.Command.OperationView> ops, string selectedId,
            IReadOnlyList<Nucleus.Core.Command.SquadView> squads = null,
            IReadOnlyDictionary<string, Vec3> unitPositions = null)
        {
            int mi = 0;
            Vec3 selLocal = default; bool haveSel = false;
            Nucleus.Core.Command.OperationView selOp = default;
            if (ops != null)
            {
                foreach (var op in ops)
                {
                    if (op.Status == Nucleus.Core.Command.OperationStatus.Complete
                        || op.Status == Nucleus.Core.Command.OperationStatus.Failed) continue;
                    var local = _projection.WorldToMapLocal(op.Position);
                    var marker = Marker(mi);
                    bool sel = op.ObjectiveId == selectedId;
                    ((RectTransform)marker.transform).localPosition = new Vector3(local.X, local.Y, 0f);
                    marker.color = ObjectiveVisuals.Color(op.Kind);
                    var lbl = Label(mi);
                    ((RectTransform)lbl.transform).localPosition = new Vector3(local.X + 10f, local.Y, 0f);
                    lbl.text = ObjectiveVisuals.Name(op.Kind);
                    lbl.color = sel ? _theme.Active : ObjectiveVisuals.Color(op.Kind);   // selection cue
                    lbl.fontSize = sel ? 13f : 11f;
                    // Contrast pill so the label reads over any terrain; sized to the text, drawn behind it.
                    var bg = LabelBg(mi);
                    var brt = (RectTransform)bg.transform;
                    brt.localPosition = new Vector3(local.X + 10f - 3f, local.Y, 0f);
                    brt.sizeDelta = new Vector2(lbl.preferredWidth + 6f, lbl.fontSize + 4f);
                    brt.SetSiblingIndex(lbl.transform.GetSiblingIndex()); // sit just behind its label
                    mi++;
                    if (sel) { selLocal = local; haveSel = true; selOp = op; }
                }
            }
            for (int i = mi; i < _markers.Count; i++) _markers[i].gameObject.SetActive(false);
            for (int i = mi; i < _objLabels.Count; i++) _objLabels[i].gameObject.SetActive(false);
            for (int i = mi; i < _labelBgs.Count; i++) _labelBgs[i].gameObject.SetActive(false);

            // For the selection, draw a status-colored line to each assigned squad's units + a cluster label
            // ("Armor Alpha · engaged"), so the player sees who is working it, not anonymous lines.
            int li = 0, sl = 0;
            if (haveSel && squads != null && unitPositions != null)
            {
                string selOpId = selOp.Id;
                foreach (var sq in squads)
                {
                    if (sq.AssignedOperationId != selOpId || sq.MemberUnitIds == null) continue;
                    var col = StatusColor(sq.Status);
                    float cx = 0f, cy = 0f; int n = 0;
                    foreach (var uid in sq.MemberUnitIds)
                    {
                        if (!unitPositions.TryGetValue(uid, out var uw)) continue;
                        var ul = _projection.WorldToMapLocal(uw);
                        DrawLine(Line(li++), new Vec3(selLocal.X, selLocal.Y, 0f), ul, col);
                        cx += ul.X; cy += ul.Y; n++;
                    }
                    if (n > 0)
                    {
                        var slbl = SquadLabel(sl++);
                        ((RectTransform)slbl.transform).localPosition = new Vector3(cx / n, cy / n + 8f, 0f);
                        slbl.text = $"{sq.Name} · {StatusPhrase(sq.Status)}";
                        slbl.color = col;
                    }
                }
            }
            for (int i = li; i < _lines.Count; i++) _lines[i].gameObject.SetActive(false);
            for (int i = sl; i < _squadLabels.Count; i++) _squadLabels[i].gameObject.SetActive(false);

            // The "why/what" header next to the selected objective: intent + phase + threat + ownership.
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
                string threat = selOp.ThreatCount > 0
                    ? $"Threat {selOp.ThreatCount}" + (selOp.ThreatAirDefense > 0 ? $" ({selOp.ThreatAirDefense} SAM)" : "")
                    : "Threat —";
                _selInfo.text = $"{ObjectiveVisuals.Name(selOp.Kind)}\n{ObjectiveVisuals.PhaseLabel(selOp.Phase)} · {ObjectiveVisuals.StatusLabel(selOp.Status)}\n{threat}\n"
                    + $"{(selOp.PlayerOwned ? "yours" : "AI")} · {selOp.SquadCount} squad{(selOp.SquadCount == 1 ? "" : "s")}";
                _selInfo.gameObject.SetActive(true);
            }
            else _selInfo.gameObject.SetActive(false);

            EnsureSelRing();
            if (haveSel)
            {
                var rt = (RectTransform)_selRing.transform;
                rt.localPosition = new Vector3(selLocal.X, selLocal.Y, 0f);
                rt.sizeDelta = new Vector2(42f, 42f);
                _selRing.gameObject.SetActive(true);
            }
            else _selRing.gameObject.SetActive(false);
        }

        // Pooled map label (objective tag + priority), drawn next to its marker.
        private TMPro.TextMeshProUGUI Label(int i)
        {
            while (_objLabels.Count <= i)
            {
                var t = UiFactory.Label("ObjLabel" + _objLabels.Count, _layer, "", 10f, Color.white);
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

        private void EnsureSelRing()
        {
            if (_selRing != null) return;
            _selRing = UiFactory.Ring("ObjSelRing", _layer, _theme.Active);
            var rt = (RectTransform)_selRing.transform;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }


        // Short phrase + color for a squad's status, so a line/label reads as "what is this squad doing".
        private static string StatusPhrase(Nucleus.Core.Command.SquadStatus s)
        {
            switch (s)
            {
                case Nucleus.Core.Command.SquadStatus.Engaged: return "engaged";
                case Nucleus.Core.Command.SquadStatus.Ready: return "en route";
                case Nucleus.Core.Command.SquadStatus.Forming: return "forming";
                case Nucleus.Core.Command.SquadStatus.Depleted: return "depleted";
                default: return "reserve";
            }
        }

        private Color StatusColor(Nucleus.Core.Command.SquadStatus s)
        {
            switch (s)
            {
                case Nucleus.Core.Command.SquadStatus.Engaged: return NativeColors.Hostile;   // in contact
                case Nucleus.Core.Command.SquadStatus.Ready: return NativeColors.Friendly;    // moving up
                case Nucleus.Core.Command.SquadStatus.Forming: return _theme.SquadForming;
                case Nucleus.Core.Command.SquadStatus.Depleted: return _theme.SquadDepleted;
                default: return _theme.SquadReserve;
            }
        }

        // Pooled label placed at a squad's unit cluster.
        private TMPro.TextMeshProUGUI SquadLabel(int i)
        {
            while (_squadLabels.Count <= i)
            {
                var t = UiFactory.Label("SquadLabel" + _squadLabels.Count, _layer, "", 10f, Color.white);
                var rt = t.rectTransform;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(140f, 16f);
                t.alignment = TMPro.TextAlignmentOptions.Center;
                _squadLabels.Add(t);
            }
            _squadLabels[i].gameObject.SetActive(true);
            return _squadLabels[i];
        }

        private void EnsureSelInfo()
        {
            if (_selInfo != null) return;
            _selInfo = UiFactory.Label("ObjSelInfo", _layer, "", 11f, _theme.Active);
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
            foreach (var l in _squadLabels) l.gameObject.SetActive(false);
            foreach (var l in _lines) l.gameObject.SetActive(false);
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
                    rt.sizeDelta = new Vector2(16f, 16f);
                }
                else rt.sizeDelta = new Vector2(11f, 11f);
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
            color.a = 0.6f;
            line.color = color;
        }
    }
}
