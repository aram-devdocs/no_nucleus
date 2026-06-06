using TMPro;
using UnityEngine;

namespace CommanderLayer.Ui
{
    /// <summary>
    /// Page-level component: the "Commander mod loaded" badge on the main menu. Resolves the game's TMP
    /// font from an existing label (so it renders correctly) and anchors a small labelled panel to a
    /// corner of the menu canvas. Returns null if no canvas is found, letting the patch fall back to IMGUI.
    /// </summary>
    public static class MainMenuBadge
    {
        public static GameObject Create(string text)
        {
            AdoptGameFont();

            Canvas canvas = FindMenuCanvas();
            if (canvas == null)
            {
                return null;
            }

            var theme = Theme.Default;
            var root = UiFactory.Panel("CommanderBadge", canvas.transform, new Color(0.06f, 0.08f, 0.10f, 0.80f));
            UiFactory.AnchorTopLeft(root, new Vector2(330f, 30f), new Vector2(16f, 16f));

            var label = UiFactory.Label("CommanderBadgeText", root, text, 15f, theme.Accent, TextAlignmentOptions.Left);
            UiFactory.Stretch(label.rectTransform);
            label.margin = new Vector4(10f, 0f, 6f, 0f);

            root.SetAsLastSibling();
            return root.gameObject;
        }

        private static void AdoptGameFont()
        {
            var labels = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
            if (labels == null)
            {
                return;
            }
            foreach (var l in labels)
            {
                if (l != null && l.font != null)
                {
                    UiFactory.Font = l.font;
                    return;
                }
            }
        }

        private static Canvas FindMenuCanvas()
        {
            var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
            if (canvases == null)
            {
                return null;
            }

            Canvas best = null;
            foreach (var c in canvases)
            {
                if (c == null || !c.isActiveAndEnabled || !c.gameObject.scene.IsValid())
                {
                    continue;
                }
                // Prefer a screen-space overlay canvas (the menu's root layer).
                if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    if (best == null || c.sortingOrder >= best.sortingOrder)
                    {
                        best = c;
                    }
                }
                else if (best == null)
                {
                    best = c;
                }
            }
            return best;
        }
    }
}
