using System.Collections.Generic;
using CommanderLayer.Core.Model;
using CommanderLayer.Core.Ports;
using UnityEngine;
using UnityEngine.UI;

namespace CommanderLayer.Ui
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
        private readonly List<Image> _lines = new List<Image>();
        private readonly List<Image> _hoverLines = new List<Image>();
        private Image _hoverRing;     // outer = unit pull radius
        private Image _hoverRingInner; // inner = area-of-operations (threat-assessment) radius
        private Image _hoverDot;

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

            SizeRing(_hoverRing, center, pullMeters, c, 0.85f);
            SizeRing(_hoverRingInner, center, aoMeters, c, 0.5f);

            var dt = (RectTransform)_hoverDot.transform;
            dt.localPosition = center;
            _hoverDot.color = canPlace ? NativeColors.Friendly : NativeColors.Hostile;
            _hoverDot.gameObject.SetActive(true);

            // Lines to the units that would be assigned.
            int hi = 0;
            if (previewUnits != null)
            {
                foreach (var uw in previewUnits)
                    DrawLine(HoverLine(hi++), local, _projection.WorldToMapLocal(uw), NativeColors.Friendly);
            }
            for (int i = hi; i < _hoverLines.Count; i++) _hoverLines[i].gameObject.SetActive(false);
        }

        private void SizeRing(Image ring, Vector3 center, float meters, Color color, float alpha)
        {
            float diam = Mathf.Clamp(2f * meters * _projection.MapScale, RingMinLocal, RingMaxLocal);
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
        }

        /// <summary>Hide all overlay graphics (e.g. when the map closes).</summary>
        public void Clear()
        {
            foreach (var m in _markers) m.gameObject.SetActive(false);
            foreach (var l in _lines) l.gameObject.SetActive(false);
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
                rt.sizeDelta = new Vector2(14f, 14f);
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
