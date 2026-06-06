namespace Nucleus.Core.Command
{
    /// <summary>
    /// Per-entity override (operation / squad): <see cref="Auto"/> = the brain drives it; <see cref="Manual"/>
    /// = the player drives that one slice (the brain yields it). Taking a "slice" = setting one operation or
    /// squad to Manual; the brain fills the rest. (Commander-level control is the two toggles on
    /// <c>CommanderState</c>, not a mode here.) <see cref="Assisted"/> is retained only as a neutral middle
    /// value for legacy data.
    /// </summary>
    public enum AutonomyLevel
    {
        Auto,
        Assisted,
        Manual
    }
}
