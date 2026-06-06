using System.Collections.Generic;
using CommanderLayer.Abstractions;
using CommanderLayer.Game.Generated;
using UnityEngine.UI;

namespace CommanderLayer.Host
{
    /// <summary>
    /// The host-owned map-bezel button registry. Mods register a <see cref="MapButtonSpec"/> in Initialize;
    /// when the MFD map opens, the host attaches each to a distinct blank bezel slot. Runs AFTER the Commander
    /// runtime's own CMD attach, and treats an already-labelled blank as taken, so it never collides with CMD
    /// and gives BLD/SQD/... their own slots. Attaches each mod's button once.
    /// </summary>
    public sealed class HostButtons : IButtonRegistry
    {
        private readonly List<MapButtonSpec> _specs = new List<MapButtonSpec>();
        private readonly HashSet<string> _attached = new HashSet<string>();
        private bool _selfTested;

        public void RegisterMapButton(MapButtonSpec spec) { if (spec != null) _specs.Add(spec); }
        public void RegisterMainMenuItem(MenuItemSpec spec) { /* main-menu items handled by MainMenuLoader */ }

        public void AttachTo(VirtualMFD mfd)
        {
            if (mfd == null || _specs.Count == 0) return;

            var blanks = new List<Button>();
            Collect(GameSdk.VirtualMFD_rightButtons(mfd), GameSdk.VirtualMFD_rightScreens(mfd), blanks);
            Collect(GameSdk.VirtualMFD_leftButtons(mfd), GameSdk.VirtualMFD_leftScreens(mfd), blanks);

            var used = new HashSet<Button>();
            foreach (var spec in _specs)
            {
                if (_attached.Contains(spec.ModId)) continue;
                Button slot = null;
                foreach (var b in blanks)
                    if (!used.Contains(b) && IsAvailable(b)) { slot = b; break; }
                if (slot == null) break; // no more blank slots
                Attach(slot, spec);
                used.Add(slot);
                _attached.Add(spec.ModId);
            }

            if (!_selfTested && _attached.Count > 0)
            {
                _selfTested = true;
                PlatformPlugin.Log?.LogInfo($"[NUCLEUS:METRIC] bezelButtons={_attached.Count}");
                PlatformPlugin.Log?.LogInfo("[NUCLEUS:SELFTEST] PASS bezel-buttons-attached");
            }
        }

        private static void Collect(List<Button> buttons, List<MFDScreen> screens, List<Button> into)
        {
            if (buttons == null) return;
            for (int i = 0; i < buttons.Count; i++)
            {
                var b = buttons[i];
                if (b == null) continue;
                bool hasScreen = screens != null && i < screens.Count && screens[i] != null;
                if (!hasScreen) into.Add(b);
            }
        }

        // A no-screen button is available only if it isn't already labelled — this skips the slot the
        // Commander runtime claimed for CMD (and any we claimed on a prior open).
        private static bool IsAvailable(Button b)
        {
            var t = b.GetComponentInChildren<Text>(true);
            return t == null || string.IsNullOrWhiteSpace(t.text);
        }

        private static void Attach(Button btn, MapButtonSpec spec)
        {
            btn.enabled = true;
            btn.gameObject.SetActive(true);
            var label = btn.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = spec.Label;
                label.enabled = true;
                label.gameObject.SetActive(true);
            }
            btn.onClick.RemoveAllListeners();
            var onClick = spec.OnClick;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
        }
    }
}
