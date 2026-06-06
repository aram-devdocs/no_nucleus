using UnityEngine;

namespace CommanderLayer.Ui
{
    /// <summary>
    /// Native HUD colors mirrored from the game's single source of truth (the codegen'd
    /// <c>NativeAssets</c> snapshot of <c>GameAssets</c>) by the composition root. The literals below are
    /// only FALLBACKS so the UI still works headless / in tests; once captured they hold the real game
    /// values. Keeps Ui decoupled from game types — Ui reads colors from here, never from GameAssets.
    /// </summary>
    public static class NativeColors
    {
        public static Color Friendly = new Color(0.45f, 0.95f, 0.55f); // fallback ← GameAssets.HUDFriendly
        public static Color Hostile = new Color(1f, 0.35f, 0.35f);     // fallback ← GameAssets.HUDHostile
        public static Color Neutral = new Color(0.85f, 0.85f, 0.55f);  // fallback ← GameAssets.HUDNeutral
        public static bool Captured;
    }
}
