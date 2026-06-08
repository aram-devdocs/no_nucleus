namespace Nucleus.Core.War
{
    /// <summary>
    /// The choices made on the pre-mission setup screen, shared (single-process) between the host's setup
    /// controller and the mods that act on them: which side the human plays, whether each side's commander is
    /// human or AI, and whether the AI auto-fills squads. Pure static config — the setup screen writes it on
    /// START, the Warfare mod applies the per-side commander kinds to its <see cref="WarState"/>, and the
    /// Commander seeds the local side's two toggles from it. <see cref="Configured"/> gates "has the player
    /// started the war yet".
    /// </summary>
    public static class WarSetup
    {
        /// <summary>The faction the human plays (null until chosen).</summary>
        public static string PlayerFaction;
        /// <summary>True once the player pressed START on the setup screen.</summary>
        public static bool Configured;
        /// <summary>Does the AI create objectives for the human's side? (the AI COMMANDER toggle, default on)</summary>
        public static bool PlayerSideAiCommander = true;
        /// <summary>Does the AI form/recruit/assign squads? (AI AUTO-FILL, default on)</summary>
        public static bool AiAutoFill = true;
        /// <summary>Commander kind for each side, keyed by faction name. The human's side is Human; the rest AI.</summary>
        public static System.Collections.Generic.Dictionary<string, CommanderKind> Commanders
            = new System.Collections.Generic.Dictionary<string, CommanderKind>();

        /// <summary>Reset to a clean slate (e.g. when a new mission loads).</summary>
        public static void Reset()
        {
            PlayerFaction = null;
            Configured = false;
            PlayerSideAiCommander = true;
            AiAutoFill = true;
            Commanders = new System.Collections.Generic.Dictionary<string, CommanderKind>();
        }
    }
}
