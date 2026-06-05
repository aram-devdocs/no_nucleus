using System.Collections.Generic;
using CommanderLayer.Core.Model;
using CommanderLayer.Core.Ports;
using UnityEngine;
using UnityEngine.UI;

namespace CommanderLayer.Ui
{
    /// <summary>
    /// Draws, on the map's icon layer, a marker per active order plus a thin line to each assigned unit,
    /// color-coded by order kind. Pooled (no per-frame allocations). Pure render from order state + a
    /// unit-position lookup + the map projection.
    /// </summary>
    public sealed class MapOverlay
    {
        private readonly Transform _layer;
        private readonly IMapProjection _projection;
        private readonly List<Image> _markers = new List<Image>();
        private readonly List<Image> _lines = new List<Image>();

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
                    Vec3 uLocal = _projection.WorldToMapLocal(up);
                    DrawLine(Line(li++), oLocal, uLocal, col);
                }
            }
            for (int i = mi; i < _markers.Count; i++) _markers[i].gameObject.SetActive(false);
            for (int i = li; i < _lines.Count; i++) _lines[i].gameObject.SetActive(false);
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
            while (_lines.Count <= i)
            {
                _lines.Add(UiFactory.LineImage("CmdLine" + _lines.Count, _layer, Color.white));
            }
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
