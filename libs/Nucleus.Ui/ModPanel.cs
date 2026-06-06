using UnityEngine;
using UnityEngine.UI;

namespace Nucleus.Ui
{
    /// <summary>
    /// The ONE way every Nucleus on-map panel is built: a fixed-size, draggable, framed container with a
    /// title/drag bar and a content area the mod fills (with a <see cref="CommanderPanel"/>). The host wraps
    /// this in a native MFD screen; CMD/BLD/SQD/WAR all use it, so they look and behave identically.
    /// </summary>
    public sealed class ModPanel
    {
        /// <summary>The fixed-size container — this is what the host toggles (the MFD screen's displayPanel).</summary>
        public RectTransform Root { get; }
        /// <summary>Where the mod renders its content (below the drag bar).</summary>
        public RectTransform Content { get; }

        public ModPanel(Transform parent, Theme theme, string title)
        {
            Root = UiFactory.Panel("NucleusPanel", parent, theme.PanelBackground);
            Root.anchorMin = new Vector2(0f, 1f); Root.anchorMax = new Vector2(0f, 1f); Root.pivot = new Vector2(0f, 1f);
            Root.sizeDelta = new Vector2(460f, 880f);
            Root.anchoredPosition = new Vector2(24f, -40f);

            var bar = UiFactory.Panel("DragBar", Root, theme.TabBackground);
            bar.anchorMin = new Vector2(0f, 1f); bar.anchorMax = new Vector2(1f, 1f); bar.pivot = new Vector2(0.5f, 1f);
            bar.sizeDelta = new Vector2(0f, 26f); bar.anchoredPosition = Vector2.zero;
            var label = UiFactory.Label("Title", bar, title + "   (drag to move)", 12f, theme.Muted, TMPro.TextAlignmentOptions.Center);
            UiFactory.Stretch(label.rectTransform);
            bar.gameObject.AddComponent<DragHandle>().Target = Root;

            Content = UiFactory.Panel("Content", Root, new Color(0f, 0f, 0f, 0f));
            Content.anchorMin = Vector2.zero; Content.anchorMax = Vector2.one;
            Content.offsetMin = Vector2.zero; Content.offsetMax = new Vector2(0f, -26f); // leave room for the drag bar

            Nucleus.Ui.Native.NativeUi.Border(Root, theme.Accent); // native frame so it reads as a game window
        }
    }
}
