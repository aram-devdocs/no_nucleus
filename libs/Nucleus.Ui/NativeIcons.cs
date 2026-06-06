using UnityEngine;

namespace CommanderLayer.Ui
{
    /// <summary>
    /// Native map/threat sprites mirrored from the game's single source of truth (the codegen'd
    /// <c>NativeAssets</c> snapshot of <c>GameAssets</c>) by the composition root. Null until captured
    /// (and stays null headless / in tests) — callers must null-check and fall back to a procedural
    /// marker. The overlay reads icons from here, never from GameAssets directly. Ready for the P6.2
    /// overlay re-base to draw native airbase/contact/threat icons instead of procedural shapes.
    /// </summary>
    public static class NativeIcons
    {
        public static Sprite Airbase;          // ← GameAssets.airbaseSprite
        public static Sprite EnemyContact;     // ← GameAssets.targetUnitSprite
        public static Sprite FriendlyContact;  // ← GameAssets.targetUnitSpriteFriendly
        public static Sprite MissileWarning;   // ← GameAssets.missileWarningSprite
        public static Sprite Warhead;          // ← GameAssets.warheadSprite
        public static bool Captured;
    }
}
