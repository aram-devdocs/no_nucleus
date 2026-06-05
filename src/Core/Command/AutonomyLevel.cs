namespace CommanderLayer.Core.Command
{
    /// <summary>
    /// How much the player has taken over a given entity (commander / operation / squad). The autonomy
    /// ladder: <see cref="Auto"/> = the AI runs it; <see cref="Assisted"/> = the AI proposes and the player
    /// confirms; <see cref="Manual"/> = the player drives it directly. Taking a "slice" = setting one entity
    /// to Manual; the AI fills the rest. Default is Auto so "do nothing = the game still runs".
    /// </summary>
    public enum AutonomyLevel
    {
        Auto,
        Assisted,
        Manual
    }

    /// <summary>
    /// The single, player-facing commander mode (one in-panel selector — no config flag). <see cref="Off"/>
    /// = the native game AI runs the war, the Commander does nothing. <see cref="Manual"/> = the Commander
    /// organizes your forces into squads and shows objectives, but issues no orders — you command by hand.
    /// <see cref="Assisted"/> = it proposes operations; nothing runs until you confirm. <see cref="Auto"/> =
    /// it runs the whole war. Manual/Assisted/Auto map to the commander's <see cref="AutonomyLevel"/>.
    /// </summary>
    public enum CommanderMode
    {
        Off,
        Manual,
        Assisted,
        Auto
    }
}
