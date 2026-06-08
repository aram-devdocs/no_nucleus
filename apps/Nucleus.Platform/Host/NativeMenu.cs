using Nucleus.Abstractions;
using Nucleus.Game.Generated;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nucleus.Host
{
    /// <summary>
    /// Adds Nucleus into the game's OWN main menu: a "NUCLEUS" button cloned from the native missions button
    /// (so it inherits the menu's exact style + sits in the menu's button layout), which toggles a panel
    /// listing the registered mods with native ON/OFF toggle buttons (cloned from the same native button) bound
    /// to <see cref="ModRegistry.SetEnabled"/>. No custom overlay canvas — everything is parented under the
    /// game's own menu canvas. Best-effort: a UI failure must never break the menu.
    /// </summary>
    public static class NativeMenu
    {
        private static GameObject _panel;

        public static void Build(MainMenu menu, ModRegistry registry)
        {
            if (menu == null || registry == null) return;
            var missions = GameSdk.MainMenu_missionsButton(menu);
            if (missions == null) { PlatformPlugin.Log?.LogWarning("[Nucleus] no native menu button to clone for NUCLEUS."); return; }

            // NUCLEUS button: clone the native missions button as a sibling, relabel, drive our toggle.
            var nucGo = Object.Instantiate(missions.gameObject, missions.transform.parent);
            nucGo.name = "NucleusMenuButton";
            nucGo.transform.SetSiblingIndex(missions.transform.GetSiblingIndex() + 1);
            SetLabel(nucGo, "NUCLEUS");
            NativeButtons.Rewire(nucGo.GetComponent<Button>(), TogglePanel);

            BuildPanel(missions, registry);

            PlatformPlugin.Log?.LogInfo("[NUCLEUS:SELFTEST] PASS menu-button-added");
            PlatformPlugin.Log?.LogInfo($"[NUCLEUS:METRIC] menuMods={registry.Count}");
        }

        private static void BuildPanel(Button template, ModRegistry registry)
        {
            var canvas = template.GetComponentInParent<Canvas>();
            var root = canvas != null ? canvas.transform : template.transform.parent;

            // Fixed width, height fits content (ContentSizeFitter) so rows never squish; a vertical layout
            // lays them out with real per-row heights. Parented under the game's own menu canvas.
            var panelGo = new GameObject("NucleusLoaderPanel",
                typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var rt = (RectTransform)panelGo.transform;
            rt.SetParent(root, worldPositionStays: false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(480f, 0f);     // width fixed; height driven by the fitter
            rt.anchoredPosition = Vector2.zero;
            panelGo.GetComponent<Image>().color = Nucleus.Ui.Theme.Default.MenuBackground;

            var vlg = panelGo.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(22, 22, 20, 20);
            vlg.spacing = 10f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var fit = panelGo.GetComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var title = new GameObject("Title", typeof(RectTransform));
            title.transform.SetParent(rt, false);
            var ttmp = title.AddComponent<TextMeshProUGUI>();
            ttmp.text = "NUCLEUS LOADER";
            ttmp.fontSize = 28f;
            ttmp.fontStyle = FontStyles.Bold;
            ttmp.alignment = TextAlignmentOptions.Center;
            Height(title, 44f);

            var hint = new GameObject("Hint", typeof(RectTransform));
            hint.transform.SetParent(rt, false);
            var htmp = hint.AddComponent<TextMeshProUGUI>();
            htmp.text = "Enable or disable mods:";
            htmp.fontSize = 16f;
            htmp.alignment = TextAlignmentOptions.Center;
            htmp.color = Nucleus.Ui.Theme.Default.MenuText;
            Height(hint, 24f);

            foreach (var mod in registry.Mods)
            {
                var id = mod.Info.Id;
                var rowGo = Object.Instantiate(template.gameObject, rt);
                rowGo.name = "ModToggle_" + id;
                Height(rowGo, 48f);
                void Refresh() => SetLabel(rowGo, $"{mod.Info.DisplayName}    {(registry.IsEnabled(id) ? "ON" : "OFF")}");
                Refresh();
                NativeButtons.Rewire(rowGo.GetComponent<Button>(), () =>
                {
                    registry.SetEnabled(id, !registry.IsEnabled(id));
                    Refresh();
                });
            }

            var closeGo = Object.Instantiate(template.gameObject, rt);
            closeGo.name = "NucleusLoaderClose";
            Height(closeGo, 48f);
            SetLabel(closeGo, "CLOSE");
            NativeButtons.Rewire(closeGo.GetComponent<Button>(), () => { if (_panel != null) _panel.SetActive(false); });

            panelGo.SetActive(false);
            _panel = panelGo;
        }

        // Pin a row to an exact height so the vertical layout never collapses it (the "squished" look).
        private static void Height(GameObject go, float h)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.preferredHeight = h; le.minHeight = h; le.flexibleHeight = 0f;
        }

        private static void TogglePanel()
        {
            if (_panel != null) _panel.SetActive(!_panel.activeSelf);
        }

        private static void SetLabel(GameObject go, string text)
        {
            // Set EVERY text component (the menu button can have more than one; the visible one isn't always
            // the first found, which is why the cloned button kept its old label).
            bool any = false;
            foreach (var tmp in go.GetComponentsInChildren<TMP_Text>(true)) { tmp.text = text; any = true; }
            foreach (var t in go.GetComponentsInChildren<Text>(true)) { t.text = text; any = true; }
            if (!any) PlatformPlugin.Log?.LogWarning($"[Nucleus] no text component on menu/bezel clone to label '{text}'.");
        }
    }
}
