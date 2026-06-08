using System.Collections.Generic;
using Nucleus.Ui;
using Cmd = Nucleus.Core.Command;
using UnityEngine;
using UnityEngine.UI;

namespace Nucleus.Composition
{
    /// <summary>World-anchored ORDER markers drawn over the cockpit while the map is CLOSED, so the player sees
    /// where the commander's orders are without opening the map. One marker per order (the goal) — drilling into
    /// an order's prerequisite child nodes is a map-only affordance. Each is a hollow, goal-kind-colored ring
    /// (distinct from the game's filled icons, so it reads as a mod cue) with a terse tag + distance, dimmed so
    /// it draws the eye without distracting. Reads the SAME <see cref="Cmd.OrderView"/> data (and cap) as the map
    /// overlay, so the two never disagree. Projected every frame from the cached HQ snapshot; edge-clamped (with
    /// a direction caret) when the order is off-screen or behind the aircraft.</summary>
    public sealed class WorldMarkerLayer
    {
        private const float EdgeMargin = 48f;

        private readonly RectTransform _root;
        private readonly Theme _theme;

        private struct Marker { public RectTransform Group; public Image Ring; public TMPro.TextMeshProUGUI Label; }
        private readonly List<Marker> _pool = new List<Marker>();

        public WorldMarkerLayer(Transform canvas, Theme theme)
        {
            _theme = theme ?? Theme.Default;
            _root = UiFactory.Panel("NucleusWorldMarkers", canvas, _theme.Transparent);
            UiFactory.Stretch(_root);
            var img = _root.GetComponent<Image>();
            if (img != null) img.raycastTarget = false;
            _root.SetAsLastSibling();
        }

        public void SetVisible(bool on)
        {
            if (_root != null && _root.gameObject.activeSelf != on) _root.gameObject.SetActive(on);
        }

        public void Render(Cmd.HqSnapshot hq)
        {
            SetVisible(true);
            var cam = Camera.main;
            var orders = hq?.Orders;
            int n = 0;
            if (cam != null && orders != null)
            {
                Vector3 camPos = cam.transform.position;
                float halfW = Screen.width * 0.5f, halfH = Screen.height * 0.5f;
                foreach (var ord in orders)
                {
                    if (n >= MapOverlay.MaxOrderMarkers) break;
                    if (ord.Status != Cmd.OrderStatus.Active) continue;

                    var gp = MapOverlay.GoalPosition(ord);   // == OrderView.Position until the goal is moved
                    var world = new Vector3(gp.X, gp.Y, gp.Z);
                    Vector3 sp = cam.WorldToScreenPoint(world);
                    bool behind = sp.z <= 0f;
                    // Behind the aircraft: WorldToScreenPoint mirrors — flip and push to the bottom so the marker
                    // still gives a "it's behind you" edge cue rather than a ghost in front.
                    Vector2 screen = behind ? new Vector2(Screen.width - sp.x, 0f) : new Vector2(sp.x, sp.y);
                    bool off = behind || screen.x < EdgeMargin || screen.x > Screen.width - EdgeMargin
                                       || screen.y < EdgeMargin || screen.y > Screen.height - EdgeMargin;
                    screen.x = Mathf.Clamp(screen.x, EdgeMargin, Screen.width - EdgeMargin);
                    screen.y = Mathf.Clamp(screen.y, EdgeMargin, Screen.height - EdgeMargin);

                    float km = Vector3.Distance(camPos, world) / 1000f;
                    var m = Ensure(n++);
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local))
                        m.Group.anchoredPosition = local;

                    var c = ObjectiveVisuals.Color(ord.GoalKind);
                    c.a = off ? 0.65f : 0.9f;                       // off-screen cues are dimmer
                    m.Ring.color = c;
                    string caret = off ? EdgeCaret(screen, halfW, halfH) : "";
                    m.Label.text = $"{ObjectiveVisuals.Tag(ord.GoalKind)} {km:0}km{caret}";
                    m.Label.color = c;
                }
            }
            for (int i = n; i < _pool.Count; i++) _pool[i].Group.gameObject.SetActive(false);
        }

        // A direction caret toward the off-screen objective, from which screen edge it was clamped to.
        private static string EdgeCaret(Vector2 screen, float halfW, float halfH)
        {
            if (screen.y <= EdgeMargin + 1f) return " v";
            if (screen.y >= 2f * halfH - EdgeMargin - 1f) return " ^";
            if (screen.x <= EdgeMargin + 1f) return " <";
            return " >";
        }

        private Marker Ensure(int i)
        {
            while (_pool.Count <= i)
            {
                var go = new GameObject("WorldMarker" + _pool.Count, typeof(RectTransform));
                var grp = (RectTransform)go.transform;
                grp.SetParent(_root, false);
                grp.anchorMin = grp.anchorMax = grp.pivot = new Vector2(0.5f, 0.5f);
                grp.sizeDelta = new Vector2(110f, 40f);

                var ring = UiFactory.Ring("Ring", grp, Color.white);
                var rrt = (RectTransform)ring.transform;
                rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0.5f, 0.5f);
                rrt.anchoredPosition = new Vector2(0f, 9f);
                rrt.sizeDelta = new Vector2(16f, 16f);

                var label = UiFactory.Label("Lbl", grp, "", 11f, Color.white, TMPro.TextAlignmentOptions.Center);
                label.enableWordWrapping = false;
                var lrt = label.rectTransform;
                lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0.5f, 0.5f);
                lrt.anchoredPosition = new Vector2(0f, -9f);
                lrt.sizeDelta = new Vector2(110f, 16f);

                _pool.Add(new Marker { Group = grp, Ring = ring, Label = label });
            }
            _pool[i].Group.gameObject.SetActive(true);
            return _pool[i];
        }
    }
}
