using System.Collections.Generic;
using Nucleus.Core.Model;
using Nucleus.Core.Ports;
using UnityEngine;
using UnityEngine.UI;

namespace Nucleus.Ui
{
    /// <summary>
    /// Map overlay on the icon layer: a marker + lines per active order (color-coded), plus a live hover
    /// range-ring while placing. Pooled, no per-frame allocations. Clear() hides everything (map closed).
    /// </summary>
    public sealed class MapOverlay
    {
        private readonly Transform _layer;
        private readonly IMapProjection _projection;
        private readonly List<Image> _markers = new List<Image>();
        private readonly List<TMPro.TextMeshProUGUI> _objLabels = new List<TMPro.TextMeshProUGUI>();
        private readonly List<TMPro.TextMeshProUGUI> _squadLabels = new List<TMPro.TextMeshProUGUI>();
        private TMPro.TextMeshProUGUI _selInfo;   // "why/what" header for the selected objective
        private readonly List<Image> _lines = new List<Image>();
        private readonly List<Image> _hoverLines = new List<Image>();
        private readonly List<Image> _hoverMarks = new List<Image>(); // highlight ring on each selected unit
        private Image _hoverRing;     // outer = unit pull radius
        private Image _hoverRingInner; // inner = area-of-operations (threat-assessment) radius
        private Image _hoverDot;
        private Image _selRing;        // ring drawn around the selected objective marker

        // Ring on-screen size is local-RectTransform units (mapDisplayFactor is a fractional scalar), clamped
        // independently per ring so neither collapses (the playtest "tiny ring") nor explodes across zoom.
        private const float RingMinLocal = 60f;
        private const float RingMaxLocal = 6000f;

        public MapOverlay(Transform iconLayer, IMapProjection projection)
        {
            _projection = projection;
            // Draw our overlay in a child container pinned as the FIRST sibling of the icon layer, so the
            // game's own unit icons/labels render ON TOP of us — our markers add context, never bury info.
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

        /// <summary>Draw a selectable marker per live objective (colored by kind), with a ring on the selected
        /// one. The objective list comes from the same operations read-model the panel renders, so the map and
        /// the panel always agree. Replaces the old range-order overlay (everything is objectives now).</summary>
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
                    marker.color = ObjectiveColor(op.Kind);
                    // A compact label so the player can read what each marker is without selecting it.
                    var lbl = Label(mi);
                    ((RectTransform)lbl.transform).localPosition = new Vector3(local.X + 10f, local.Y, 0f);
                    lbl.text = $"{KindTag(op.Kind)} P{op.Priority:0.#}";
                    lbl.color = sel ? NativeColors.Friendly : ObjectiveColor(op.Kind);
                    lbl.fontSize = sel ? 13f : 11f;
                    mi++;
                    if (sel) { selLocal = local; haveSel = true; selOp = op; }
                }
            }
            for (int i = mi; i < _markers.Count; i++) _markers[i].gameObject.SetActive(false);
            for (int i = mi; i < _objLabels.Count; i++) _objLabels[i].gameObject.SetActive(false);

            // When an objective is selected, draw a line from it to every unit of its assigned squads — colored
            // by squad status — and a label at each squad's cluster ("Armor Alpha · engaged"), so the player
            // sees exactly WHO is working it and WHAT they're doing, not just anonymous lines.
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
                _selInfo.text = $"{KindName(selOp.Kind)}\n{selOp.Phase} · {selOp.Status}\n{threat}\n"
                    + $"P{selOp.Priority:0.#} · {(selOp.PlayerOwned ? "you" : "AI")} · {selOp.SquadCount} sq";
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

        // A short tag per objective kind for the map label.
        private static string KindTag(Nucleus.Core.Command.ObjectiveKind kind)
        {
            switch (kind)
            {
                case Nucleus.Core.Command.ObjectiveKind.CapturePoint: return "CAP";
                case Nucleus.Core.Command.ObjectiveKind.DestroyTarget: return "DESTROY";
                case Nucleus.Core.Command.ObjectiveKind.DefendArea: return "DEFEND";
                case Nucleus.Core.Command.ObjectiveKind.ControlAirspace: return "AIR";
                case Nucleus.Core.Command.ObjectiveKind.Resupply: return "SUPPLY";
                default: return "RECON";
            }
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

        // A distinct color per objective kind so the map reads at a glance.
        private static Color ObjectiveColor(Nucleus.Core.Command.ObjectiveKind kind)
        {
            switch (kind)
            {
                case Nucleus.Core.Command.ObjectiveKind.CapturePoint: return new Color(0.4f, 0.8f, 1f);
                case Nucleus.Core.Command.ObjectiveKind.DestroyTarget: return new Color(1f, 0.45f, 0.4f);
                case Nucleus.Core.Command.ObjectiveKind.DefendArea: return new Color(0.45f, 0.9f, 0.55f);
                case Nucleus.Core.Command.ObjectiveKind.ControlAirspace: return new Color(0.7f, 0.6f, 1f);
                case Nucleus.Core.Command.ObjectiveKind.Resupply: return new Color(1f, 0.85f, 0.4f);
                default: return Color.white; // Recon
            }
        }

        private void EnsureSelRing()
        {
            if (_selRing != null) return;
            _selRing = UiFactory.Ring("ObjSelRing", _layer, NativeColors.Friendly);
            var rt = (RectTransform)_selRing.transform;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        // Full readable name for the selected-objective header (vs. the terse per-marker KindTag).
        private static string KindName(Nucleus.Core.Command.ObjectiveKind kind)
        {
            switch (kind)
            {
                case Nucleus.Core.Command.ObjectiveKind.CapturePoint: return "Capture point";
                case Nucleus.Core.Command.ObjectiveKind.DestroyTarget: return "Destroy target";
                case Nucleus.Core.Command.ObjectiveKind.DefendArea: return "Defend area";
                case Nucleus.Core.Command.ObjectiveKind.ControlAirspace: return "Control airspace";
                case Nucleus.Core.Command.ObjectiveKind.Resupply: return "Resupply";
                default: return "Recon";
            }
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

        private static Color StatusColor(Nucleus.Core.Command.SquadStatus s)
        {
            switch (s)
            {
                case Nucleus.Core.Command.SquadStatus.Engaged: return NativeColors.Hostile;          // in contact
                case Nucleus.Core.Command.SquadStatus.Ready: return NativeColors.Friendly;           // moving up
                case Nucleus.Core.Command.SquadStatus.Forming: return new Color(0.6f, 0.8f, 1f);
                case Nucleus.Core.Command.SquadStatus.Depleted: return new Color(0.6f, 0.6f, 0.6f);  // hurt
                default: return new Color(0.5f, 0.55f, 0.6f);                                        // reserve
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
            _selInfo = UiFactory.Label("ObjSelInfo", _layer, "", 11f, NativeColors.Friendly);
            var rt = _selInfo.rectTransform;
            rt.pivot = new Vector2(0f, 1f);                 // top-left anchored to the marker
            rt.sizeDelta = new Vector2(150f, 64f);
            _selInfo.alignment = TMPro.TextAlignmentOptions.TopLeft;
        }

        public void Render(IReadOnlyList<OrderState> orders, IReadOnlyDictionary<string, Vec3> unitPositions)
        {
            int mi = 0, li = 0;
            foreach (var o in orders)
            {
                if (o.Status == OrderStatus.Complete) continue;
                Color col = OrderColors.For(o.Order.Kind);
                Vec3 oLocal = _projection.WorldToMapLocal(o.Order.Position);

                var marker = Marker(mi++);
                ((RectTransform)marker.transform).localPosition = new Vector3(oLocal.X, oLocal.Y, 0f);
                marker.color = col;

                foreach (var id in o.AssignedUnitIds)
                {
                    if (!unitPositions.TryGetValue(id, out var up)) continue;
                    DrawLine(Line(li++), oLocal, _projection.WorldToMapLocal(up), col);
                }
            }
            for (int i = mi; i < _markers.Count; i++) _markers[i].gameObject.SetActive(false);
            for (int i = li; i < _lines.Count; i++) _lines[i].gameObject.SetActive(false);
        }

        /// <summary>
        /// Show the placement preview at a world point: an outer ring (unit pull radius), an inner ring
        /// (area of operations / threat radius), a centre dot, and a faint line to every unit that would be
        /// assigned (<paramref name="previewUnits"/>) so the player sees exactly who responds.
        /// </summary>
        public void SetHover(Vec3 world, float pullMeters, float aoMeters, OrderKind kind, bool canPlace,
            IReadOnlyList<Vec3> previewUnits = null)
        {
            EnsureHover();
            Vec3 local = _projection.WorldToMapLocal(world);
            var center = new Vector3(local.X, local.Y, 0f);
            Color c = canPlace ? OrderColors.For(kind) : NativeColors.Hostile;

            // Inner ring gets a smaller floor than the outer so the two don't collapse into one when zoomed out.
            SizeRing(_hoverRing, center, pullMeters, c, 0.85f, RingMinLocal);
            SizeRing(_hoverRingInner, center, aoMeters, c, 0.5f, RingMinLocal * 0.55f);

            var dt = (RectTransform)_hoverDot.transform;
            dt.localPosition = center;
            _hoverDot.color = canPlace ? NativeColors.Friendly : NativeColors.Hostile;
            _hoverDot.gameObject.SetActive(true);

            // Lines AND a bright highlight ring on every unit that would be assigned, so the player clearly
            // sees exactly who responds before committing.
            int hi = 0;
            if (previewUnits != null)
            {
                foreach (var uw in previewUnits)
                {
                    var u = _projection.WorldToMapLocal(uw);
                    DrawLine(HoverLine(hi), local, u, NativeColors.Friendly);
                    var mk = HoverMark(hi);
                    ((RectTransform)mk.transform).localPosition = new Vector3(u.X, u.Y, 0f);
                    mk.color = NativeColors.Friendly;
                    hi++;
                }
            }
            for (int i = hi; i < _hoverLines.Count; i++) _hoverLines[i].gameObject.SetActive(false);
            for (int i = hi; i < _hoverMarks.Count; i++) _hoverMarks[i].gameObject.SetActive(false);
        }

        // A bright ring drawn over a unit that the pending order will select.
        private Image HoverMark(int i)
        {
            while (_hoverMarks.Count <= i)
            {
                var img = UiFactory.Ring("CmdHoverMark" + _hoverMarks.Count, _layer, Color.white, dashed: false);
                var rt = (RectTransform)img.transform;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(26f, 26f);
                _hoverMarks.Add(img);
            }
            _hoverMarks[i].gameObject.SetActive(true);
            return _hoverMarks[i];
        }

        private void SizeRing(Image ring, Vector3 center, float meters, Color color, float alpha, float minLocal)
        {
            float diam = Mathf.Clamp(2f * meters * _projection.MapScale, minLocal, RingMaxLocal);
            var rt = (RectTransform)ring.transform;
            rt.localPosition = center;
            rt.sizeDelta = new Vector2(diam, diam);
            color.a = alpha; ring.color = color;
            ring.gameObject.SetActive(true);
        }

        public void ClearHover()
        {
            if (_hoverRing != null) _hoverRing.gameObject.SetActive(false);
            if (_hoverRingInner != null) _hoverRingInner.gameObject.SetActive(false);
            if (_hoverDot != null) _hoverDot.gameObject.SetActive(false);
            foreach (var l in _hoverLines) l.gameObject.SetActive(false);
            foreach (var m in _hoverMarks) m.gameObject.SetActive(false);
        }

        /// <summary>Hide all overlay graphics (e.g. when the map closes).</summary>
        public void Clear()
        {
            foreach (var m in _markers) m.gameObject.SetActive(false);
            foreach (var l in _objLabels) l.gameObject.SetActive(false);
            foreach (var l in _squadLabels) l.gameObject.SetActive(false);
            foreach (var l in _lines) l.gameObject.SetActive(false);
            if (_selRing != null) _selRing.gameObject.SetActive(false);
            if (_selInfo != null) _selInfo.gameObject.SetActive(false);
            ClearHover();
        }

        private void EnsureHover()
        {
            if (_hoverRing == null) _hoverRing = UiFactory.Ring("CmdHoverRing", _layer, Color.white, dashed: true);
            if (_hoverRingInner == null) _hoverRingInner = UiFactory.Ring("CmdHoverRingInner", _layer, Color.white, dashed: true);
            if (_hoverDot == null)
            {
                _hoverDot = UiFactory.LineImage("CmdHoverDot", _layer, Color.white);
                var rt = (RectTransform)_hoverDot.transform;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(10f, 10f);
            }
        }

        private Image HoverLine(int i)
        {
            while (_hoverLines.Count <= i) _hoverLines.Add(UiFactory.LineImage("CmdHoverLine" + _hoverLines.Count, _layer, Color.white));
            _hoverLines[i].gameObject.SetActive(true);
            return _hoverLines[i];
        }

        private Image Marker(int i)
        {
            while (_markers.Count <= i)
            {
                var img = UiFactory.LineImage("CmdMarker" + _markers.Count, _layer, Color.white);
                var rt = (RectTransform)img.transform;
                rt.pivot = new Vector2(0.5f, 0.5f);
                // Use the game's own target sprite so the marker reads as native iconography (tinted by the
                // order color). Kept small so a cluster of objectives doesn't bury the map (was 20px).
                if (NativeIcons.Warhead != null)
                {
                    img.sprite = NativeIcons.Warhead;
                    img.preserveAspect = true;
                    rt.sizeDelta = new Vector2(16f, 16f);   // a touch larger so objectives read at map zoom
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
