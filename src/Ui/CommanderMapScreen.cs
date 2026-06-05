using System;
using CommanderLayer.Core.Model;
using UnityEngine;

namespace CommanderLayer.Ui
{
    /// <summary>
    /// Template: the Commander panel (the "modal"), built on our overlay canvas. It starts hidden and is
    /// opened/closed by the native CMD map button. The map marker overlay is a separate organism owned by
    /// the runtime.
    /// </summary>
    public sealed class CommanderMapScreen
    {
        private readonly CommanderPanel _panel;
        private readonly RectTransform _container;
        private bool _open;

        public CommanderMapScreen(Transform parent, Theme theme, Action onArmPlace, Action onClear)
        {
            _container = UiFactory.Panel("CommanderScreen", parent, new Color(0f, 0f, 0f, 0f));
            UiFactory.AnchorTopLeft(_container, new Vector2(340f, 470f), new Vector2(90f, 90f));

            _panel = new CommanderPanel(_container, theme, onArmPlace, onClear);
            var p = _panel.Root;
            p.anchorMin = Vector2.zero;
            p.anchorMax = Vector2.one;
            p.offsetMin = Vector2.zero;
            p.offsetMax = Vector2.zero;

            SetOpen(false);
        }

        public void Toggle() => SetOpen(!_open);

        public void SetOpen(bool open)
        {
            _open = open;
            _panel.SetVisible(open);
        }

        public void Render(CommanderState state) => _panel.Render(state);
    }
}
