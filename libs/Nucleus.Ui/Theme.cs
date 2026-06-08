using Nucleus.Core.Model;
using UnityEngine;

namespace Nucleus.Ui
{
    /// <summary>
    /// Centralized palette so atoms never hardcode colors. <see cref="Accent"/> mirrors the local faction
    /// color (a game value). The panel-chrome colors (backgrounds, text, muted, state cues) are MOD-OWNED
    /// — the mod's own window styling, with no game equivalent — not copies of game colors. Native HUD
    /// colors/icons live in <see cref="NativeColors"/> / <see cref="NativeIcons"/>.
    /// </summary>
    public sealed class Theme
    {
        public Color PanelBackground { get; }
        public Color TabBackground { get; }
        public Color ButtonIdle { get; }
        public Color Text { get; }
        public Color Muted { get; }
        public Color Accent { get; }
        /// <summary>Reserved for DESTRUCTIVE actions only (REMOVE) — so red never means "selected/active".</summary>
        public Color Danger { get; }
        /// <summary>The single on/selected/active cue (toggles ON, selected rows, open bezel) — green, never red.</summary>
        public Color Active { get; }
        public Color EnRoute { get; }
        public Color Arrived { get; }
        public Color ObjectiveMarker { get; }

        // Attrition scoreboard.
        public Color ScoreBlufor { get; }
        public Color ScoreOpfor { get; }
        public Color BarTrack { get; }
        /// <summary>Over-commit / depleted warning text (e.g. funds below zero, a hurt squad).</summary>
        public Color WarnText { get; }

        // Squad-status cues not sourced from the game's affiliation palette (Engaged/EnRoute use NativeColors).
        public Color SquadForming { get; }
        public Color SquadDepleted { get; }
        public Color SquadReserve { get; }

        // Overlay / HUD chrome.
        public Color LabelBackdrop { get; }
        public Color HudBackground { get; }
        public Color HudText { get; }
        public Color ScrollbarTrack { get; }
        public Color BadgeBackground { get; }
        public Color MenuBackground { get; }
        public Color MenuText { get; }
        /// <summary>Fully transparent — for invisible layout containers (viewport, content host).</summary>
        public Color Transparent { get; }

        public Theme(Color accent)
        {
            Accent = accent;
            PanelBackground = new Color(0.06f, 0.08f, 0.10f, 0.92f);
            TabBackground = new Color(0.10f, 0.13f, 0.16f, 0.95f);
            ButtonIdle = new Color(0.18f, 0.22f, 0.27f, 1f);
            Text = new Color(0.92f, 0.94f, 0.96f, 1f);
            Muted = new Color(0.62f, 0.66f, 0.70f, 1f);
            Danger = new Color(0.85f, 0.34f, 0.34f, 1f);
            Active = new Color(0.30f, 0.85f, 0.45f, 1f);
            EnRoute = new Color(1f, 0.78f, 0.30f, 1f);
            Arrived = new Color(0.45f, 0.95f, 0.55f, 1f);
            ObjectiveMarker = new Color(1f, 0.85f, 0.20f, 1f);

            ScoreBlufor = new Color(0.35f, 0.6f, 1f, 1f);
            ScoreOpfor = new Color(1f, 0.45f, 0.4f, 1f);
            BarTrack = new Color(0.15f, 0.15f, 0.18f, 1f);
            WarnText = new Color(1f, 0.5f, 0.5f, 1f);

            SquadForming = new Color(0.6f, 0.8f, 1f, 1f);
            SquadDepleted = new Color(0.6f, 0.6f, 0.6f, 1f);
            SquadReserve = new Color(0.5f, 0.55f, 0.6f, 1f);

            LabelBackdrop = new Color(0f, 0f, 0f, 0.5f);
            HudBackground = new Color(0.04f, 0.06f, 0.08f, 0.88f);
            HudText = new Color(0.8f, 0.85f, 0.9f, 1f);
            ScrollbarTrack = new Color(0f, 0f, 0f, 0.35f);
            BadgeBackground = new Color(0.06f, 0.08f, 0.10f, 0.80f);
            MenuBackground = new Color(0.05f, 0.07f, 0.09f, 0.97f);
            MenuText = new Color(0.7f, 0.78f, 0.85f, 1f);
            Transparent = new Color(0f, 0f, 0f, 0f);
        }

        public static Theme FromFaction(FactionInfo faction)
        {
            // Convert the Unity-free ColorRgba to a UnityEngine.Color here, at the UI boundary (the conversion
            // is a UI concern, so Nucleus.Ui stays independent of the engine-access lib).
            Color accent = faction != null
                ? new Color(faction.Color.R, faction.Color.G, faction.Color.B, faction.Color.A)
                : new Color(0.3f, 0.7f, 1f);
            return new Theme(accent);
        }

        public static Theme Default => new Theme(new Color(0.3f, 0.7f, 1f));
    }
}
