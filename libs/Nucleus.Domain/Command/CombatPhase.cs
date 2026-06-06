namespace CommanderLayer.Core.Command
{
    /// <summary>The combined-arms phases an operation against a defended target steps through, in order.</summary>
    public enum CombatPhase { Recon, AirSuperiority, Sead, Strike, Assault, Capture, Hold }

    /// <summary>Friendly force facts the gates threshold against (extended as more are wired through).</summary>
    public readonly struct ForceState
    {
        /// <summary>Air-superiority fighters available to the operation.</summary>
        public readonly int Fighters;
        public ForceState(int fighters) { Fighters = fighters; }
    }
}
