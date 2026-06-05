using System.Collections.Generic;
using CommanderLayer.Core.Model;
using CommanderLayer.Core.Ports;
using UnityEngine;
using UnityEngine.UI;

namespace CommanderLayer.Ui
{
    /// <summary>
    /// Organism: renders the objective marker and a line to each assigned unit, on the map's icon layer.
    /// Pure render from CommanderState + an IMapProjection; no game lookups of its own.
    /// </summary>
    public sealed class MapOverlay
    {
        private readonly Theme _theme;
        private readonly IMapProjection _projection;
        private readonly Transform _layer;
        private readonly Image _marker;
        private readonly List<Image> _lines = new List<Image>();

        public MapOverlay(Transform iconLayer, Theme theme, IMapProjection projection)
        {
            _layer = iconLayer;
            _theme = theme;
            _projection = projection;

            _marker = UiFactory.LineImage("CommanderObjectiveMarker", _layer, theme.ObjectiveMarker);
            var rt = (RectTransform)_marker.transform;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(14f, 14f);
            _marker.gameObject.SetActive(false);
        }

        public void Render(CommanderState state)
        {
            if (_marker == null)
            {
                return;
            }

            if (state == null || state.Objective == null)
            {
                _marker.gameObject.SetActive(false);
                HideLinesFrom(0);
                return;
            }

            Vec3 objLocal = _projection.WorldToMapLocal(state.Objective.Position);
            ((RectTransform)_marker.transform).localPosition = new Vector3(objLocal.X, objLocal.Y, 0f);
            _marker.gameObject.SetActive(true);

            var units = state.Assignments.Units;
            EnsureLines(units.Count);
            for (int i = 0; i < _lines.Count; i++)
            {
                if (i < units.Count)
                {
                    Vec3 unitLocal = _projection.WorldToMapLocal(units[i].Position);
                    DrawLine(_lines[i], objLocal, unitLocal,
                        units[i].State == AssignmentState.Arrived ? _theme.Arrived : _theme.EnRoute);
                    _lines[i].gameObject.SetActive(true);
                }
                else
                {
                    _lines[i].gameObject.SetActive(false);
                }
            }
        }

        private static void DrawLine(Image line, Vec3 a, Vec3 b, Color color)
        {
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;

            var rt = (RectTransform)line.transform;
            rt.localPosition = new Vector3(a.X, a.Y, 0f);
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
            rt.sizeDelta = new Vector2(length, 2f);
            line.color = color;
        }

        private void EnsureLines(int count)
        {
            while (_lines.Count < count)
            {
                _lines.Add(UiFactory.LineImage("CommanderLine" + _lines.Count, _layer, _theme.EnRoute));
            }
        }

        private void HideLinesFrom(int index)
        {
            for (int i = index; i < _lines.Count; i++)
            {
                _lines[i].gameObject.SetActive(false);
            }
        }
    }
}
