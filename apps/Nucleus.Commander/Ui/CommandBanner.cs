using System;
using Nucleus.Ui;
using UnityEngine;
using UnityEngine.UI;

namespace Nucleus.Composition
{
    /// <summary>A one-time, top-center notice shown WHILE FLYING when the AI is commanding the player's side:
    /// "AI is commanding your side — fly freely, or Take Over any node". Click to dismiss (the dismissed flag is
    /// UI-local and sticky for the session). Lives on the same screen-space overlay canvas as the flight HUD.</summary>
    public sealed class CommandBanner
    {
        private readonly RectTransform _root;

        public CommandBanner(Transform canvas, Theme theme, Action onDismiss)
        {
            var t = theme ?? Theme.Default;
            var btn = UiFactory.Button("NucleusCmdBanner", canvas,
                UiStrings.AiCommandingBanner + UiStrings.AiCommandingBannerDismiss, t,
                () => onDismiss?.Invoke());
            _root = (RectTransform)btn.transform;
            _root.anchorMin = _root.anchorMax = _root.pivot = new Vector2(0.5f, 1f); // top-center
            _root.sizeDelta = new Vector2(UiTokens.BannerWidth, UiTokens.BannerHeight);
            // Sit clear of the top-center war strip (war score + next action), not on top of it.
            _root.anchoredPosition = new Vector2(0f, -(UiTokens.WarStripHeight + UiTokens.PadEdge));
            _root.GetComponent<Image>().color = t.BadgeBackground;

            var label = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (label != null) { label.color = t.Accent; label.fontSize = UiTokens.FontBody; }
            _root.SetAsLastSibling();
        }

        public void SetVisible(bool on)
        {
            if (_root != null && _root.gameObject.activeSelf != on) _root.gameObject.SetActive(on);
        }
    }
}
