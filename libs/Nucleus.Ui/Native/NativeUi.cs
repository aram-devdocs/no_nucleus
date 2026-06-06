using UnityEngine;

namespace CommanderLayer.Ui.Native
{
    /// <summary>Hosts the game's own UI components so our panels read as native.</summary>
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
    }
}
