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
        private Image _hoverRing;
        private Image _hoverDot;

        public MapOverlay(Transform iconLayer, IMapProjection projection)
        {
            _layer = iconLayer;
            _projection = projection;
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

        /// <summary>Show the placement ring (range) at a world point, tinted by kind / can-place.</summary>
        public void SetHover(Vec3 world, float rangeMeters, OrderKind kind, bool canPlace)
        {
            EnsureHover();
            Vec3 local = _projection.WorldToMapLocal(world);
            float diam = 2f * rangeMeters * _projection.MapScale;

            var rt = (RectTransform)_hoverRing.transform;
            rt.localPosition = new Vector3(local.X, local.Y, 0f);
            rt.sizeDelta = new Vector2(diam, diam);
            Color c = canPlace ? OrderColors.For(kind) : new Color(1f, 0.35f, 0.35f);
            c.a = 0.85f; _hoverRing.color = c;
            _hoverRing.gameObject.SetActive(true);

            var dt = (RectTransform)_hoverDot.transform;
            dt.localPosition = new Vector3(local.X, local.Y, 0f);
            _hoverDot.color = c;
            _hoverDot.gameObject.SetActive(true);
        }

        public void ClearHover()
        {
            if (_hoverRing != null) _hoverRing.gameObject.SetActive(false);
            if (_hoverDot != null) _hoverDot.gameObject.SetActive(false);
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
            if (_hoverRing == null)
            {
                _hoverRing = UiFactory.Ring("CmdHoverRing", _layer, Color.white);
            }
            if (_hoverDot == null)
            {
                _hoverDot = UiFactory.LineImage("CmdHoverDot", _layer, Color.white);
                var rt = (RectTransform)_hoverDot.transform;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(10f, 10f);
            }
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
