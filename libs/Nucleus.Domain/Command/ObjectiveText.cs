namespace Nucleus.Core.Command
{
    /// <summary>
    /// Single source of truth for how an <see cref="ObjectiveKind"/> / <see cref="CombatPhase"/> reads to the
    /// player, as plain strings — pure and engine-free, so the brain's battle feed (Nucleus.Campaign) and the
    /// UI (Nucleus.Ui.ObjectiveVisuals, which adds the colors) word them identically. Without this the pure
    /// layers leaked raw PascalCase enum names ("DestroyTarget", "Sead") next to the UI's "Destroy target",
    /// "SEAD".
    /// </summary>
    public static class ObjectiveText
    {
        /// <summary>Full readable name: "Capture point", "Destroy target", …</summary>
        public static string Name(ObjectiveKind kind)
        {
            switch (kind)
            {
                case ObjectiveKind.CapturePoint:    return "Capture point";
                case ObjectiveKind.DestroyTarget:   return "Destroy target";
                case ObjectiveKind.DefendArea:      return "Defend area";
                case ObjectiveKind.ControlAirspace: return "Control airspace";
                case ObjectiveKind.Resupply:        return "Resupply";
                case ObjectiveKind.SuppressAirDefense: return "Suppress air defense";
                case ObjectiveKind.NavalStrike:     return "Naval strike";
                default:                            return "Recon";
            }
        }

        /// <summary>Terse tag for tight contexts: CAP / DESTROY / DEFEND / AIR / SUPPLY / RECON / SEAD / NAVAL.</summary>
        public static string Tag(ObjectiveKind kind)
        {
            switch (kind)
            {
                case ObjectiveKind.CapturePoint:    return "CAP";
                case ObjectiveKind.DestroyTarget:   return "DESTROY";
                case ObjectiveKind.DefendArea:      return "DEFEND";
                case ObjectiveKind.ControlAirspace: return "AIR";
                case ObjectiveKind.Resupply:        return "SUPPLY";
                case ObjectiveKind.SuppressAirDefense: return "SEAD";
                case ObjectiveKind.NavalStrike:     return "NAVAL";
                default:                            return "RECON";
            }
        }

        /// <summary>Readable combat-phase label: "SEAD" / "Air superiority" / "Scouting" instead of the raw enum.</summary>
        public static string PhaseLabel(CombatPhase phase)
        {
            switch (phase)
            {
                case CombatPhase.Recon:          return "Scouting";
                case CombatPhase.AirSuperiority: return "Air superiority";
                case CombatPhase.Sead:           return "SEAD";
                case CombatPhase.Strike:         return "Strike";
                case CombatPhase.Assault:        return "Assault";
                case CombatPhase.Capture:        return "Capturing";
                case CombatPhase.Hold:           return "Holding";
                default:                         return phase.ToString();
            }
        }
    }
}
