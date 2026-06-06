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

            var panelGo = new GameObject("NucleusModsPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            var rt = (RectTransform)panelGo.transform;
            rt.SetParent(root, worldPositionStays: false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(380f, 70f + 40f * registry.Count);
            rt.anchoredPosition = Vector2.zero;
            panelGo.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.09f, 0.96f);
            var vlg = panelGo.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 12, 12);
            vlg.spacing = 8f;
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var title = new GameObject("Title", typeof(RectTransform));
            title.transform.SetParent(rt, false);
            var ttmp = title.AddComponent<TextMeshProUGUI>();
            ttmp.text = "NUCLEUS MODS";
            ttmp.fontSize = 22f;
            ttmp.alignment = TextAlignmentOptions.Center;

            foreach (var mod in registry.Mods)
            {
                var id = mod.Info.Id;
                var rowGo = Object.Instantiate(template.gameObject, rt);
                rowGo.name = "ModToggle_" + id;
                void Refresh() => SetLabel(rowGo, $"{mod.Info.DisplayName}:  {(registry.IsEnabled(id) ? "ON" : "OFF")}");
                Refresh();
                NativeButtons.Rewire(rowGo.GetComponent<Button>(), () =>
                {
                    registry.SetEnabled(id, !registry.IsEnabled(id));
                    Refresh();
                });
            }

            panelGo.SetActive(false);
            _panel = panelGo;
        }

        private static void TogglePanel()
        {
            if (_panel != null) _panel.SetActive(!_panel.activeSelf);
        }

        private static void SetLabel(GameObject go, string text)
        {
            var tmp = go.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) { tmp.text = text; return; }
            var t = go.GetComponentInChildren<Text>(true);
            if (t != null) t.text = text;
        }
    }
}
