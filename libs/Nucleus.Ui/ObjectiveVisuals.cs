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
                default:                                return new Color(0.85f, 0.85f, 0.85f); // Recon
            }
        }

        /// <summary>Hex (RRGGBB) derived from <see cref="Color"/> — for TMP rich-text bullets. Never hand-typed.</summary>
        public static string Hex(Cmd.ObjectiveKind kind) => ColorUtility.ToHtmlStringRGB(Color(kind));

        /// <summary>Terse tag for tight contexts (map marker label / HUD row): CAP / DESTROY / DEFEND / AIR / SUPPLY / RECON.</summary>
        public static string Tag(Cmd.ObjectiveKind kind)
        {
            switch (kind)
            {
                case Cmd.ObjectiveKind.CapturePoint:    return "CAP";
                case Cmd.ObjectiveKind.DestroyTarget:   return "DESTROY";
                case Cmd.ObjectiveKind.DefendArea:      return "DEFEND";
                case Cmd.ObjectiveKind.ControlAirspace: return "AIR";
                case Cmd.ObjectiveKind.Resupply:        return "SUPPLY";
                default:                                return "RECON";
            }
        }

        /// <summary>Readable label for a combat phase — so every surface (HUD/map/panel) says "SEAD" /
        /// "Air superiority" / "Scouting" instead of the raw PascalCase enum ("Sead", "AirSuperiority").
        /// SSOT for phase wording, mirroring how the brain narrates phases in the feed.</summary>
        public static string PhaseLabel(Cmd.CombatPhase phase)
        {
            switch (phase)
            {
                case Cmd.CombatPhase.Recon:          return "Scouting";
                case Cmd.CombatPhase.AirSuperiority: return "Air superiority";
                case Cmd.CombatPhase.Sead:           return "SEAD";
                case Cmd.CombatPhase.Strike:         return "Strike";
                case Cmd.CombatPhase.Assault:        return "Assault";
                case Cmd.CombatPhase.Capture:        return "Capturing";
                case Cmd.CombatPhase.Hold:           return "Holding";
                default:                             return phase.ToString();
            }
        }

        /// <summary>Full readable name for headers/labels: "Capture point", "Destroy target", …</summary>
        public static string Name(Cmd.ObjectiveKind kind)
        {
            switch (kind)
            {
                case Cmd.ObjectiveKind.CapturePoint:    return "Capture point";
                case Cmd.ObjectiveKind.DestroyTarget:   return "Destroy target";
                case Cmd.ObjectiveKind.DefendArea:      return "Defend area";
                case Cmd.ObjectiveKind.ControlAirspace: return "Control airspace";
                case Cmd.ObjectiveKind.Resupply:        return "Resupply";
                default:                                return "Recon";
            }
        }
    }
}
