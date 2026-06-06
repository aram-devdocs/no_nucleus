using System.Collections.Generic;
using Nucleus.Abstractions;
using Nucleus.Game.Generated;
using UnityEngine;
using UnityEngine.UI;

namespace Nucleus.Host
{
    /// <summary>
    /// The host-owned map-bezel button registry. Mods register a <see cref="MapButtonSpec"/> in Initialize;
    /// when the MFD map opens, the host adds each as a NATIVE bezel button by cloning one of the game's own
    /// bezel buttons — so it inherits the game's exact style and is placed by the game's own layout group
    /// (responsive by construction), then appending it to the MFD's button list so the game shows/hides it
    /// with the rest (<c>ToggleAllButtons</c>). No reflection slot-hijacking, no custom canvas. Each mod's
    /// button is added once.
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

            // Prefer the right bezel; fall back to the left. Clone one of its real buttons as the template.
            var buttons = GameSdk.VirtualMFD_rightButtons(mfd);
            var screens = GameSdk.VirtualMFD_rightScreens(mfd);
            int rightCount = buttons?.Count ?? -1;
            if (buttons == null || buttons.Count == 0)
            {
                buttons = GameSdk.VirtualMFD_leftButtons(mfd);
                screens = GameSdk.VirtualMFD_leftScreens(mfd);
            }
            if (!_hookSeen)
            {
                _hookSeen = true;
                PlatformPlugin.Log?.LogInfo($"[NUCLEUS:METRIC] mfdMaximizeHook=1 rightButtons={rightCount} chosenButtons={(buttons?.Count ?? -1)}");
            }
            if (buttons == null || buttons.Count == 0) return;

            var template = FirstReal(buttons);
            if (template == null) return;

            foreach (var spec in _specs)
            {
                if (_attached.Contains(spec.ModId)) continue;

                var go = Object.Instantiate(template.gameObject, template.transform.parent);
                go.name = "NucleusBezel_" + spec.ModId;
                var btn = go.GetComponent<Button>();
                if (btn == null) { Object.Destroy(go); continue; }

                var label = go.GetComponentInChildren<Text>(true);
                if (label != null) { label.text = spec.Label; label.enabled = true; label.gameObject.SetActive(true); }

                // The cloned persistent onClick still points at the template's PressButton(index) — drop it and
                // drive the mod directly. (Panels move into native MFD screens in the next phase.)
                btn.onClick.RemoveAllListeners();
                var onClick = spec.OnClick;
                if (onClick != null) btn.onClick.AddListener(() => onClick());
                btn.enabled = true;
                go.SetActive(true);

                // Register with the MFD so the game's ToggleAllButtons manages our button's visibility too.
                // Keep the parallel screens list aligned (null = no native screen ⇒ PressButton is a no-op).
                buttons.Add(btn);
                screens?.Add(null);
                _attached.Add(spec.ModId);
            }

            if (!_selfTested && _attached.Count > 0)
            {
                _selfTested = true;
                PlatformPlugin.Log?.LogInfo($"[NUCLEUS:METRIC] bezelButtons={_attached.Count}");
                PlatformPlugin.Log?.LogInfo("[NUCLEUS:SELFTEST] PASS bezel-buttons-attached");
            }
        }

        private static Button FirstReal(List<Button> buttons)
        {
            foreach (var b in buttons) if (b != null) return b;
            return null;
        }
    }
}
