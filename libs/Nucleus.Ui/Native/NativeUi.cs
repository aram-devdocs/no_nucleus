using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Nucleus.Ui.Native
{
    /// <summary>Composes the game's own UI chrome so the mod's panels read as native: frame a panel with the
    /// game's procedural border, and borrow its sliced button sprite. Returns null / no-ops when no live template
    /// exists (headless), so callers fall back to a built atom.</summary>
    public static class NativeUi
    {
        /// <summary>Frame a panel with the game's procedural border (accent edges, transparent fill).</summary>
        public static void Border(RectTransform panel, Color accent, float thickness = 2f)
        {
            if (panel == null) return;
            var go = new GameObject("NativeBorder", typeof(RectTransform));
            go.transform.SetParent(panel, false);
            UiFactory.Stretch((RectTransform)go.transform);
            var border = go.AddComponent<NuclearOption.UI.BetterBorder>();
            border.BorderThickness = thickness;
            border.color = new Color(accent.r, accent.g, accent.b, 0.9f);
            border.FillColor = new Color(0f, 0f, 0f, 0f);
            border.raycastTarget = false;
        }

        private static Button _btnTpl; private static bool _btnSearched;

        // A live, sliced-sprite Button (the game's chrome). Harvested once and cached.
        private static Button ButtonTemplate()
        {
            if (!_btnSearched)
            {
                _btnTpl = Resources.FindObjectsOfTypeAll<Button>().FirstOrDefault(b =>
                    b != null && b.gameObject.scene.IsValid() && b.image != null
                    && b.image.sprite != null && b.image.type == Image.Type.Sliced);
                _btnSearched = true;
            }
            return _btnTpl;
        }

        /// <summary>The game's sliced button chrome sprite, so built atoms match the native look. Null when no
        /// live template exists yet (headless / pre-scene).</summary>
        public static Sprite? SlicedButtonSprite() => ButtonTemplate()?.image?.sprite;
    }
}
