using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CommanderLayer.Ui.Native
{
    /// <summary>
    /// The native-UI component library (P6): the mod's controls ARE the game's own components wherever we
    /// can clone/host them, so the Commander UI reads as native — same border, buttons, toggles. Every method
    /// degrades GRACEFULLY: if the native component isn't available it falls back to the hand-rolled
    /// <see cref="UiFactory"/> atom (which itself now reads native font/colors via the codegen'd
    /// <c>NativeAssets</c> snapshot), so a missing/changed game type can never blank a panel.
    ///
    /// Status: <see cref="Border"/> hosts the game's real <c>NuclearOption.UI.BetterBorder</c> (proven in the
    /// live panel frame). <see cref="Button"/> currently delegates to the atom; the runtime HARVEST of native
    /// Button/BoxToggle/SliderToggle prefabs (clone + rebind) is P6.2 render work, gated on the
    /// <c>[S0:UI]</c> harvest probe telling us which instances are cloneable. The contract test already
    /// guards those native types exist (manifest), so this library extends without guesswork once we have
    /// the probe log.
    /// </summary>
    public static class NativeUi
    {
        /// <summary>
        /// Frame <paramref name="panel"/> with the game's own procedural border (<c>BetterBorder</c>) so it
        /// reads as a native window: accent-colored edges, transparent fill, non-raycasting. Returns the
        /// border component, or null if the game type wasn't available (caller simply gets no frame — never
        /// an exception). This is the proven path used by the live Commander panel.
        /// </summary>
        public static NuclearOption.UI.BetterBorder Border(RectTransform panel, Color accent, float thickness = 2f)
        {
            if (panel == null) return null;
            try
            {
                var go = new GameObject("NativeBorder", typeof(RectTransform));
                go.transform.SetParent(panel, false);
                UiFactory.Stretch((RectTransform)go.transform);
                var border = go.AddComponent<NuclearOption.UI.BetterBorder>();
                border.BorderThickness = thickness;
                border.color = new Color(accent.r, accent.g, accent.b, 0.9f); // border = faction accent
                border.FillColor = new Color(0f, 0f, 0f, 0f);                 // transparent fill (panel bg shows)
                border.raycastTarget = false;
                return border;
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning("NativeUi.Border skipped: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// A button styled to read as native. Currently a graceful fallback to <see cref="UiFactory.Button"/>
        /// (which uses the harvested native button sprite + native font); the real native-prefab clone lands
        /// with the P6.2 harvest once the probe confirms a cloneable source. Signature is the target shape so
        /// callers don't change when the clone path is wired in behind it.
        /// </summary>
        public static Button Button(string name, Transform parent, string text, Theme theme, UnityAction onClick)
            => UiFactory.Button(name, parent, text, theme, onClick);
    }
}
