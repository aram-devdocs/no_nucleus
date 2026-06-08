using UnityEngine;
using Cmd = Nucleus.Core.Command;

namespace Nucleus.Ui
{
    /// <summary>
    /// Single source of truth for how an <see cref="Cmd.ObjectiveKind"/> reads in the UI: its color, short tag,
    /// and full name. The hex string is DERIVED from the color (ColorUtility), so a row's rich-text dot and a
    /// map marker can never drift apart. Every panel / map overlay / HUD uses these instead of its own copy —
    /// change a color once, everywhere updates.
    /// </summary>
    public static class ObjectiveVisuals
    {
        /// <summary>The kind's color (the one place it's defined).</summary>
        public static Color Color(Cmd.ObjectiveKind kind)
        {
            switch (kind)
            {
                case Cmd.ObjectiveKind.CapturePoint:    return new Color(0.40f, 0.80f, 1.00f);
                case Cmd.ObjectiveKind.DestroyTarget:   return new Color(1.00f, 0.45f, 0.40f);
                case Cmd.ObjectiveKind.DefendArea:      return new Color(0.45f, 0.90f, 0.55f);
                case Cmd.ObjectiveKind.ControlAirspace: return new Color(0.70f, 0.60f, 1.00f);
                case Cmd.ObjectiveKind.Resupply:        return new Color(1.00f, 0.85f, 0.40f);
                case Cmd.ObjectiveKind.SuppressAirDefense: return new Color(1.00f, 0.40f, 0.76f); // magenta #FF66C2
                case Cmd.ObjectiveKind.NavalStrike:     return new Color(0.20f, 0.77f, 0.77f);    // teal #33C4C4
                default:                                return new Color(0.85f, 0.85f, 0.85f); // Recon
            }
        }

        /// <summary>Hex (RRGGBB) derived from <see cref="Color"/> — for TMP rich-text bullets. Never hand-typed.</summary>
        public static string Hex(Cmd.ObjectiveKind kind) => ColorUtility.ToHtmlStringRGB(Color(kind));

        // Wording is the pure-Domain SSOT (Cmd.ObjectiveText) so the brain's feed and the UI read identically;
        // this Ui type owns only the colors.
        /// <summary>Terse tag for tight contexts (map marker label / HUD row): CAP / DESTROY / DEFEND / AIR / SUPPLY / RECON.</summary>
        public static string Tag(Cmd.ObjectiveKind kind) => Cmd.ObjectiveText.Tag(kind);

        /// <summary>Readable label for a combat phase — "SEAD" / "Air superiority" / "Scouting".</summary>
        public static string PhaseLabel(Cmd.CombatPhase phase) => Cmd.ObjectiveText.PhaseLabel(phase);

        /// <summary>Full readable name for headers/labels: "Capture point", "Destroy target", …</summary>
        public static string Name(Cmd.ObjectiveKind kind) => Cmd.ObjectiveText.Name(kind);

        /// <summary>Readable operation status (not the raw enum): Planning → "Forming up", etc.</summary>
        public static string StatusLabel(Cmd.OperationStatus s) => Cmd.OperationText.StatusLabel(s);
    }
}
