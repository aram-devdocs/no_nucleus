using UnityEngine;

namespace Nucleus.Ui
{
    /// <summary>Native map/threat sprites mirrored from the codegen'd <c>NativeAssets</c> snapshot by the
    /// composition root. Null until captured (and stays null headless) — callers must null-check and fall back
    /// to a procedural marker.</summary>
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
