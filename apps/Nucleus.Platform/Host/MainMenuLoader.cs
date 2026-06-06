using CommanderLayer.Abstractions;
using CommanderLayer.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CommanderLayer.Host
{
    /// <summary>
    /// The main-menu mod loader: a "MODS" button that opens a panel listing every registered mod with a
    /// per-mod ON/OFF toggle (host-driven enable/disable at runtime). Lives in the app because it needs the
    /// ModRegistry (the generic Ui lib must not reference Abstractions). Best-effort — a UI failure must never
    /// break menu load. Emits a [NUCLEUS:SELFTEST] line so a playtest auto-verifies the loader appeared.
    /// </summary>
    public static class MainMenuLoader
    {
        private static GameObject _panel;

        public static void Build(ModRegistry registry)
        {
            if (registry == null) return;
            var canvas = MainMenuBadge.FindMenuCanvas();
            if (canvas == null) return;
            var theme = Theme.Default;

            var button = UiFactory.Button("NucleusModsButton", canvas.transform, "MODS", theme, Toggle);
            UiFactory.AnchorTopLeft((RectTransform)button.transform, new Vector2(90f, 26f), new Vector2(16f, 52f));
            ((RectTransform)button.transform).SetAsLastSibling();

            var panel = UiFactory.Panel("NucleusModsPanel", canvas.transform, new Color(0.06f, 0.08f, 0.10f, 0.93f));
            UiFactory.AnchorTopLeft(panel, new Vector2(260f, 30f + 28f * registry.Count), new Vector2(16f, 84f));
            UiFactory.VerticalLayout("NucleusModsList", panel, 4f, new RectOffset(8, 8, 8, 8));

            var header = UiFactory.Label("NucleusModsHeader", panel, "NUCLEUS MODS", 13f, theme.Accent, TextAlignmentOptions.Left);
            UiFactory.PreferredHeight(header.gameObject, 18f);

            foreach (var mod in registry.Mods)
            {
                var id = mod.Info.Id;
                var row = UiFactory.Panel($"row-{id}", panel, theme.TabBackground);
                UiFactory.PreferredHeight(row.gameObject, 24f);
                UiFactory.HorizontalLayout($"rowL-{id}", row, 6f);

                UiFactory.Label($"name-{id}", row, mod.Info.DisplayName, 13f, theme.Text, TextAlignmentOptions.Left);

                Button toggle = null;
                toggle = UiFactory.Button($"toggle-{id}", row, registry.IsEnabled(id) ? "ON" : "OFF", theme, () =>
                {
                    registry.SetEnabled(id, !registry.IsEnabled(id));
                    var lbl = toggle.GetComponentInChildren<TextMeshProUGUI>();
                    if (lbl != null) lbl.text = registry.IsEnabled(id) ? "ON" : "OFF";
                });
                UiFactory.Fixed(toggle.gameObject, 46f, 22f);
            }

            panel.SetAsLastSibling();
            panel.gameObject.SetActive(false);
            _panel = panel.gameObject;

            PlatformPlugin.Log?.LogInfo($"[NUCLEUS:SELFTEST] PASS loader-ui-built");
            PlatformPlugin.Log?.LogInfo($"[NUCLEUS:METRIC] loaderMods={registry.Count}");
        }

        private static void Toggle()
        {
            if (_panel != null) _panel.SetActive(!_panel.activeSelf);
        }
    }
}
