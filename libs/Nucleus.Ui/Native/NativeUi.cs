using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Nucleus.Ui.Native
{
    /// <summary>
    /// The codegenned native-UI kit: instead of hand-rolling uGUI, the mod COMPOSES the game's own UI widgets
    /// so its panels ARE the game's UI (same font/sprites/borders/toggle behaviour). Each factory finds a live
    /// instance of a native widget (the proven harvest idiom — Resources.FindObjectsOfTypeAll&lt;T&gt; filtered to
    /// scene-valid), clones it, and rewires its events (dropping the prefab's persistent listeners so the clone
    /// does ONLY what we ask). The widget TYPES are guarded by the codegen contract manifest
    /// (NuclearOption.UI.BoxToggle / BetterBorder / BetterToggleGroup / BaseToggle) — a game rename fails the
    /// contract test, not the panel. Every method degrades gracefully (returns null) when no live template is
    /// available (e.g. headless), so callers fall back to a built atom.
    /// </summary>
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

        // ---- Native template harvest (find once per widget type; null when unavailable, e.g. headless) ----

        private static T FindLiveTemplate<T>() where T : Component
        {
            foreach (var c in Resources.FindObjectsOfTypeAll<T>())
                if (c != null && c.gameObject != null && c.gameObject.scene.IsValid()) return c;
            return null;
        }

        private static NuclearOption.UI.BoxToggle _boxTpl; private static bool _boxSearched;
        private static Button _btnTpl; private static bool _btnSearched;

        /// <summary>A native game toggle (BoxToggle) — same look + animation as the game's options. Sets its
        /// label, its initial state without firing, and rewires onValueChanged to <paramref name="onChanged"/>.
        /// Returns null if no live template exists yet (caller should fall back to a built control).</summary>
        public static NuclearOption.UI.BoxToggle Toggle(Transform parent, string label, bool isOn, Action<bool> onChanged)
        {
            if (!_boxSearched) { _boxTpl = FindLiveTemplate<NuclearOption.UI.BoxToggle>(); _boxSearched = true; }
            if (_boxTpl == null) return null;

            var clone = UnityEngine.Object.Instantiate(_boxTpl.gameObject, parent).GetComponent<NuclearOption.UI.BoxToggle>();
            clone.name = "NativeToggle";
            SetLabel(clone.gameObject, label);
            clone.SetIsOnWithoutNotify(isOn);
            RewireBool(clone.onValueChanged, onChanged);
            clone.gameObject.SetActive(true);
            return clone;
        }

        // A "real" button: a Button whose image is a sliced sprite (the game's chrome), live in a scene.
        // Harvested once and cached (shared by Button() and SlicedButtonSprite()).
        private static Button ButtonTemplate()
        {
            if (!_btnSearched)
            {
                _btnTpl = Resources.FindObjectsOfTypeAll<Button>().FirstOrDefault(b =>
                    b != null && b.gameObject.scene.IsValid() && b.image != null
                    && b.image.sprite != null && b.image.type == Image.Type.Sliced);
                _btnSearched = true;
            }
            return _btnTpl;
        }

        /// <summary>The game's sliced button chrome sprite (from the live button template), so built atoms can
        /// match the native look. Null when no live template exists yet (headless / pre-scene).</summary>
        public static Sprite SlicedButtonSprite() => ButtonTemplate()?.image?.sprite;

        /// <summary>A native game button (cloned from a live sliced-sprite Button), label + onClick rewired.
        /// Returns null if no live template exists yet (caller should fall back to UiFactory.Button).</summary>
        public static Button Button(Transform parent, string label, Action onClick)
        {
            if (ButtonTemplate() == null) return null;

            var clone = UnityEngine.Object.Instantiate(_btnTpl.gameObject, parent).GetComponent<Button>();
            clone.name = "NativeButton";
            SetLabel(clone.gameObject, label);
            var e = clone.onClick;
            for (int i = e.GetPersistentEventCount() - 1; i >= 0; i--) e.SetPersistentListenerState(i, UnityEventCallState.Off);
            e.RemoveAllListeners();
            if (onClick != null) e.AddListener(new UnityAction(() => onClick()));
            clone.enabled = true; clone.interactable = true; clone.gameObject.SetActive(true);
            return clone;
        }

        // Drop a cloned widget's prefab-wired (persistent) + runtime listeners, then install ours — same idiom
        // as NativeButtons.Rewire, but for a UnityEvent&lt;bool&gt; (BaseToggle.onValueChanged).
        private static void RewireBool(UnityEvent<bool> ev, Action<bool> onChanged)
        {
            if (ev == null) return;
            for (int i = ev.GetPersistentEventCount() - 1; i >= 0; i--) ev.SetPersistentListenerState(i, UnityEventCallState.Off);
            ev.RemoveAllListeners();
            if (onChanged != null) ev.AddListener(new UnityAction<bool>(v => onChanged(v)));
        }

        // Set a cloned widget's text label (prefer TMP, fall back to legacy Text).
        private static void SetLabel(GameObject root, string label)
        {
            if (root == null || label == null) return;
            var tmp = root.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null) { tmp.text = label; return; }
            var t = root.GetComponentInChildren<Text>(true);
            if (t != null) { t.text = label; t.enabled = true; }
        }
    }
}
