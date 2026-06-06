using System.Collections.Generic;
using Nucleus.Abstractions;
using Nucleus.Game.Generated;
using UnityEngine;
using UnityEngine.UI;

namespace Nucleus.Host
{
    /// <summary>
    /// The host-owned map-bezel registry. Each registered mod becomes a NATIVE MFD bezel button paired with a
    /// native <c>MFDScreen</c>: the button is cloned from one of the game's own bezel buttons (native look) and
    /// repositioned into a free bezel slot (so they don't stack); the screen is built fresh (no game scripts to
    /// misfire) with the green "open" highlight cloned from a real screen. Wiring the button through the game's
    /// <c>PressLeftButton/PressRightButton</c> means the game drives toggle + highlight, and hides it on
    /// <c>onMapMinimized</c> — so the mod's panel closes with the map. The mod fills its screen via
    /// <see cref="MapButtonSpec.BuildContent"/>. Each mod is added once.
    /// </summary>
    public sealed class HostButtons : IButtonRegistry
    {
        private readonly List<MapButtonSpec> _specs = new List<MapButtonSpec>();
        private readonly HashSet<string> _attached = new HashSet<string>();
        private bool _selfTested;
        private bool _hookSeen;

        public void RegisterMapButton(MapButtonSpec spec) { if (spec != null) _specs.Add(spec); }
        public void RegisterMainMenuItem(MenuItemSpec spec) { /* main-menu items handled natively in the menu */ }

        public void AttachTo(VirtualMFD mfd)
        {
            if (mfd == null || _specs.Count == 0) return;

            var leftButtons = GameSdk.VirtualMFD_leftButtons(mfd);
            var leftScreens = GameSdk.VirtualMFD_leftScreens(mfd);
            var rightButtons = GameSdk.VirtualMFD_rightButtons(mfd);
            var rightScreens = GameSdk.VirtualMFD_rightScreens(mfd);

            if (!_hookSeen)
            {
                _hookSeen = true;
                PlatformPlugin.Log?.LogInfo($"[NUCLEUS:METRIC] mfdMaximizeHook=1 leftButtons={leftButtons?.Count ?? -1} rightButtons={rightButtons?.Count ?? -1}");
            }

            var screenTemplate = FirstNonNull(leftScreens) ?? FirstNonNull(rightScreens);
            if (screenTemplate == null) { PlatformPlugin.Log?.LogWarning("[Nucleus] no native MFDScreen to model — cannot add bezel screens."); return; }

            // Pre-compute placement on each side from the existing native buttons (added below the last one).
            var leftSlot = new SlotPlacer(leftButtons);
            var rightSlot = new SlotPlacer(rightButtons);
            bool nextLeft = (leftButtons?.Count ?? 0) <= (rightButtons?.Count ?? 0);

            foreach (var spec in _specs)
            {
                if (_attached.Contains(spec.ModId)) continue;
                try
                {
                    bool left = nextLeft;
                    var buttons = left ? leftButtons : rightButtons;
                    var screens = left ? leftScreens : rightScreens;
                    var placer = left ? leftSlot : rightSlot;
                    if (buttons == null || screens == null) { left = !left; buttons = left ? leftButtons : rightButtons; screens = left ? leftScreens : rightScreens; placer = left ? leftSlot : rightSlot; }
                    if (buttons == null || screens == null) continue;

                    var template = FirstNonNull(buttons);
                    if (template == null) continue;

                    // 1. Native-looking button: clone a real one, reposition into a free slot.
                    var btnGo = Object.Instantiate(template.gameObject, template.transform.parent);
                    btnGo.name = "NucleusBezel_" + spec.ModId;
                    var btn = btnGo.GetComponent<Button>();
                    if (btn == null) { Object.Destroy(btnGo); continue; }
                    placer.Place((RectTransform)btnGo.transform);
                    var btnText = btnGo.GetComponentInChildren<Text>(true);
                    if (btnText != null) { btnText.text = spec.Label; btnText.enabled = true; btnText.gameObject.SetActive(true); }

                    // 2. Fresh native MFDScreen modelled on a real one (placement/show/hide/highlight by the game).
                    var screen = BuildScreen(mfd, screenTemplate, spec.Label, btnText, out var content);

                    // 3. Register the pair at the same index so PressButton(index) toggles our screen.
                    buttons.Add(btn);
                    screens.Add(screen);

                    bool onLeft = left;
                    var extra = spec.OnClick;
                    // Drop the clone's inherited persistent onClick (it would toggle the template's screen) and
                    // drive ours: the game's PressButton toggles OUR paired screen (native highlight + hide).
                    NativeButtons.Rewire(btn, () =>
                    {
                        if (onLeft) mfd.PressLeftButton(btn); else mfd.PressRightButton(btn);
                        extra?.Invoke();
                    });
                    btn.enabled = true;
                    btnGo.SetActive(true);

                    // 4. Let the mod fill its screen once.
                    spec.BuildContent?.Invoke(content);

                    _attached.Add(spec.ModId);
                    nextLeft = !nextLeft;
                }
                catch (System.Exception e)
                {
                    PlatformPlugin.Log?.LogError($"[Nucleus] failed to add bezel for '{spec.ModId}': {e}");
                }
            }

            if (!_selfTested && _attached.Count > 0)
            {
                _selfTested = true;
                PlatformPlugin.Log?.LogInfo($"[NUCLEUS:METRIC] bezelButtons={_attached.Count}");
                PlatformPlugin.Log?.LogInfo("[NUCLEUS:SELFTEST] PASS bezel-buttons-attached");
            }
        }

        // Build a fresh MFDScreen GameObject modelled on `src`: same transform footprint, a stretched content
        // panel for the mod's UI, and the green highlight cloned from `src`. Returns the screen; outs the
        // content RectTransform the mod renders into.
        private static MFDScreen BuildScreen(VirtualMFD mfd, MFDScreen src, string label, Text bezelLabel, out RectTransform content)
        {
            var srcRt = (RectTransform)src.transform;
            var go = new GameObject("NucleusScreen_" + label, typeof(RectTransform));
            go.transform.SetParent(srcRt.parent, worldPositionStays: false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = srcRt.anchorMin; rt.anchorMax = srcRt.anchorMax; rt.pivot = srcRt.pivot;
            rt.sizeDelta = srcRt.sizeDelta; rt.anchoredPosition = srcRt.anchoredPosition;
            rt.localScale = srcRt.localScale; rt.localRotation = srcRt.localRotation;

            // Content panel (the mod's UI parent), stretched to fill the screen footprint.
            var contentGo = new GameObject("Content", typeof(RectTransform));
            content = (RectTransform)contentGo.transform;
            content.SetParent(rt, worldPositionStays: false);
            content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one;
            content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero;

            var screen = go.AddComponent<MFDScreen>();
            screen.displayPanel = contentGo;
            screen.aircraftOnly = false;
            screen.label = bezelLabel;   // Setup() writes shortName into the bezel button's label

            // Green "open" highlight: clone the native one so it matches exactly.
            if (src.highlight != null)
            {
                var hl = Object.Instantiate(src.highlight.gameObject, rt);
                hl.name = "Highlight";
                screen.highlight = hl.GetComponent<Image>();
            }

            screen.Setup(mfd, label);

            // Start hidden until the bezel button is pressed.
            contentGo.SetActive(false);
            if (screen.highlight != null) screen.highlight.enabled = false;
            screen.isActive = false;
            return screen;
        }

        private static Button FirstNonNull(List<Button> list)
        {
            if (list != null) foreach (var b in list) if (b != null) return b;
            return null;
        }

        private static MFDScreen FirstNonNull(List<MFDScreen> list)
        {
            if (list != null) foreach (var s in list) if (s != null) return s;
            return null;
        }

        // Places cloned buttons into successive free bezel slots, continuing the native column's spacing.
        private sealed class SlotPlacer
        {
            private readonly Vector2 _base;
            private readonly Vector2 _pitch;
            private int _added;

            public SlotPlacer(List<Button> buttons)
            {
                if (buttons == null || buttons.Count == 0) { _base = Vector2.zero; _pitch = new Vector2(0f, -46f); return; }
                var last = (RectTransform)buttons[buttons.Count - 1].transform;
                _base = last.anchoredPosition;
                if (buttons.Count >= 2)
                {
                    var a = (RectTransform)buttons[0].transform;
                    var b = (RectTransform)buttons[1].transform;
                    var d = b.anchoredPosition - a.anchoredPosition;
                    _pitch = d.sqrMagnitude > 1f ? d : new Vector2(0f, -(last.rect.height + 6f));
                }
                else _pitch = new Vector2(0f, -(last.rect.height + 6f));
            }

            public void Place(RectTransform rt)
            {
                _added++;
                rt.anchoredPosition = _base + _pitch * _added;
            }
        }
    }
}
