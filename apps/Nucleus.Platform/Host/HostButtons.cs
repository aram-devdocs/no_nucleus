using System.Collections.Generic;
using Nucleus.Abstractions;
using Nucleus.Game.Generated;
using UnityEngine;
using UnityEngine.UI;

namespace Nucleus.Host
{
    /// <summary>The host-owned map-bezel registry. Each mod becomes a native MFD bezel button (cloned from a
    /// game button, placed in a free slot) paired with a fresh <c>MFDScreen</c>. Wiring through the game's
    /// <c>PressLeftButton/PressRightButton</c> lets the game drive toggle/highlight and hide on map-minimize, so
    /// the panel closes with the map. The mod fills its screen via <see cref="MapButtonSpec.BuildContent"/>.</summary>
    public sealed class HostButtons : IButtonRegistry
    {
        private readonly List<MapButtonSpec> _specs = new List<MapButtonSpec>();
        private readonly HashSet<string> _attached = new HashSet<string>();
        // Button + paired screen, to keep the "open" tint synced to the screen's actual isActive each frame:
        // the game closes a same-side screen without re-running the first button's handler, so a one-shot tint desyncs.
        private struct Tint { public Image Img; public MFDScreen Screen; public Color Open, Closed; }
        private readonly List<Tint> _tints = new List<Tint>();
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
                PlatformPlugin.Log?.LogInfo("[NUCLEUS:PROBE] HostButtons build=P8.5-probe");
                PlatformPlugin.Log?.LogInfo($"[NUCLEUS:PROBE] nativeCounts L.btn={leftButtons?.Count ?? -1} L.scr={leftScreens?.Count ?? -1} R.btn={rightButtons?.Count ?? -1} R.scr={rightScreens?.Count ?? -1}");
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

                    // 3. Register the pair so PressButton(button)'s index lands on OUR screen. The button/screen
                    //    lists can differ in length (blank buttons have no screen), so pad screens to the button
                    //    index first — else the lookup hits the wrong screen and the button appears dead.
                    buttons.Add(btn);
                    int idx = buttons.Count - 1;
                    while (screens.Count < idx) screens.Add(null);
                    screens.Add(screen);

                    // PROBE: verify, right now, that PressButton(ourBtn) will resolve to OUR screen.
                    bool aligned = idx < screens.Count && ReferenceEquals(screens[idx], screen)
                                   && (left ? leftButtons : rightButtons).IndexOf(btn) == idx;
                    PlatformPlugin.Log?.LogInfo($"[NUCLEUS:PROBE] add id={spec.ModId} side={(left ? "L" : "R")} idx={idx} aligned={aligned} hasHighlight={screen.highlight != null} btnInteractable={btn.interactable} btnCount={buttons.Count} scrCount={screens.Count}");

                    bool onLeft = left;
                    var extra = spec.OnClick;
                    var capScreen = screen;
                    var capId = spec.ModId;
                    var capImg = btn.image != null ? btn.image : btn.GetComponent<Image>();
                    var capOrig = capImg != null ? capImg.color : Color.white;
                    var capGreen = Nucleus.Ui.Theme.Default.Active;   // one canonical "open/active" cue
                    // Drop the clone's inherited onClick (it would toggle the template's screen); drive ours via
                    // PressButton, and tint green while open.
                    NativeButtons.Rewire(btn, () =>
                    {
                        PlatformPlugin.Log?.LogInfo($"[NUCLEUS:PROBE] click id={capId} beforeActive={capScreen.isActive}");
                        if (onLeft) mfd.PressLeftButton(btn); else mfd.PressRightButton(btn);
                        if (capImg != null) capImg.color = capScreen.isActive ? capGreen : capOrig;
                        PlatformPlugin.Log?.LogInfo($"[NUCLEUS:PROBE] click id={capId} afterActive={capScreen.isActive} highlightOn={(capScreen.highlight != null && capScreen.highlight.enabled)} content={(capScreen.displayPanel != null && capScreen.displayPanel.activeSelf)}");
                        extra?.Invoke();
                    });
                    btn.enabled = true;
                    btn.interactable = true;
                    btnGo.SetActive(true);
                    // Track for the per-frame tint sync (fixes the "first button still shows open" desync).
                    if (capImg != null) _tints.Add(new Tint { Img = capImg, Screen = capScreen, Open = capGreen, Closed = capOrig });

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

        // Build a fresh MFDScreen modelled on `src`. A content area STRETCHED to the screen rect resolves to
        // ZERO size and renders nothing; only a FIXED-SIZE panel renders — so the displayPanel is a fixed-size
        // panel the mod fills, framed by the green highlight. Outs the content rect.
        private static MFDScreen BuildScreen(VirtualMFD mfd, MFDScreen src, string label, Text bezelLabel, out RectTransform content)
        {
            var srcRt = (RectTransform)src.transform;
            var go = new GameObject("NucleusScreen_" + label, typeof(RectTransform));
            go.transform.SetParent(srcRt.parent, worldPositionStays: false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = srcRt.anchorMin; rt.anchorMax = srcRt.anchorMax; rt.pivot = srcRt.pivot;
            rt.sizeDelta = srcRt.sizeDelta; rt.anchoredPosition = srcRt.anchoredPosition;
            rt.localScale = srcRt.localScale; rt.localRotation = srcRt.localRotation; rt.localPosition = srcRt.localPosition;

            // The ONE standard panel chrome (fixed-size, draggable, framed) — same for every mod. Its Root is
            // the displayPanel the game toggles; the mod fills its Content.
            var panel = new Nucleus.Ui.ModPanel(rt, Nucleus.Ui.Theme.Default, label);
            content = panel.Content;

            var screen = go.AddComponent<MFDScreen>();
            screen.displayPanel = panel.Root.gameObject;   // ShowScreen toggles the whole fixed-size panel
            screen.aircraftOnly = false;
            screen.label = bezelLabel;                       // Setup() writes shortName into the bezel button's label

            // Green "open" highlight: clone the native one (matches the game's colour) and frame the panel.
            if (src.highlight != null)
            {
                var hl = Object.Instantiate(src.highlight.gameObject, panel.Root);
                hl.name = "Highlight";
                var hlrt = (RectTransform)hl.transform;
                hlrt.anchorMin = Vector2.zero; hlrt.anchorMax = Vector2.one;
                hlrt.offsetMin = Vector2.zero; hlrt.offsetMax = Vector2.zero;
                var hlImg = hl.GetComponent<Image>();
                if (hlImg != null) hlImg.raycastTarget = false;
                hl.transform.SetAsLastSibling(); // draw the frame over the content
                screen.highlight = hlImg;
            }

            screen.Setup(mfd, label);

            // Start hidden until the bezel button is pressed.
            panel.Root.gameObject.SetActive(false);
            if (screen.highlight != null) screen.highlight.enabled = false;
            screen.isActive = false;
            return screen;
        }

        /// <summary>Sync each bezel button's tint to its screen's actual open/closed state (called each frame),
        /// so when the game closes one screen to open another, the closed button stops showing "open".</summary>
        public void RefreshTints()
        {
            for (int i = 0; i < _tints.Count; i++)
            {
                var t = _tints[i];
                if (t.Img == null || t.Screen == null) continue;
                var want = t.Screen.isActive ? t.Open : t.Closed;
                if (t.Img.color != want) t.Img.color = want;
            }
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
