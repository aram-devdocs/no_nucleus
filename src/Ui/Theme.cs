using CommanderLayer.Core.Model;
using CommanderLayer.Game;
using UnityEngine;

namespace CommanderLayer.Ui
{
    /// <summary>
    /// Centralized palette so atoms never hardcode colors. Accent derives from the local faction color.
    /// </summary>
    public sealed class Theme
    {
        public Color PanelBackground { get; }
        public Color TabBackground { get; }
        public Color ButtonIdle { get; }
        public Color Text { get; }
        public Color Muted { get; }
        public Color Accent { get; }
        public Color EnRoute { get; }
        public Color Arrived { get; }
        public Color ObjectiveMarker { get; }

        public Theme(Color accent)
        {
            Accent = accent;
            PanelBackground = new Color(0.06f, 0.08f, 0.10f, 0.92f);
            TabBackground = new Color(0.10f, 0.13f, 0.16f, 0.95f);
            ButtonIdle = new Color(0.18f, 0.22f, 0.27f, 1f);
            Text = new Color(0.92f, 0.94f, 0.96f, 1f);
            Muted = new Color(0.62f, 0.66f, 0.70f, 1f);
            EnRoute = new Color(1f, 0.78f, 0.30f, 1f);
            Arrived = new Color(0.45f, 0.95f, 0.55f, 1f);
            ObjectiveMarker = new Color(1f, 0.85f, 0.20f, 1f);
        }

        public static Theme FromFaction(FactionInfo faction)
        {
            Color accent = faction != null ? GameConvert.ToColor(faction.Color) : new Color(0.3f, 0.7f, 1f);
            return new Theme(accent);
        }

        public static Theme Default => new Theme(new Color(0.3f, 0.7f, 1f));
    }
}
