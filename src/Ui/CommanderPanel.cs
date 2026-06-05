using System;
using System.Collections.Generic;
using CommanderLayer.Core.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CommanderLayer.Ui
{
    /// <summary>
    /// Organism: the Commander tab content. Pure presentation — it renders from a CommanderState passed to
    /// Render() and reports intent through the onArmPlace / onClear callbacks. It holds no game references.
    /// </summary>
    public sealed class CommanderPanel
    {
        private readonly Theme _theme;
        private readonly RectTransform _root;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _status;
        private readonly TextMeshProUGUI _unitsHeader;
        private readonly Transform _unitsContainer;
        private readonly Button _placeButton;
        private readonly List<TextMeshProUGUI> _rows = new List<TextMeshProUGUI>();

        public RectTransform Root => _root;

        public CommanderPanel(Transform parent, Theme theme, Action onArmPlace, Action onClear)
        {
            _theme = theme;
            _root = UiFactory.Panel("CommanderPanel", parent, theme.PanelBackground);

            var layout = UiFactory.VerticalLayout("CommanderPanel_Layout", _root, 6f, new RectOffset(10, 10, 10, 10));
            UiFactory.Stretch((RectTransform)layout.transform);

            _title = UiFactory.Label("Title", layout.transform, "COMMANDER", 18f, theme.Accent);
            UiFactory.PreferredHeight(_title.gameObject, 24f);

            _status = UiFactory.Label("Status", layout.transform, "", 13f, theme.Muted);
            UiFactory.PreferredHeight(_status.gameObject, 36f);

            var buttons = UiFactory.VerticalLayout("Buttons", layout.transform, 6f, new RectOffset(0, 0, 0, 0));
            _placeButton = UiFactory.Button("PlaceButton", buttons.transform, "Place Objective", theme, () => onArmPlace?.Invoke());
            UiFactory.PreferredHeight(_placeButton.gameObject, 30f);
            var clear = UiFactory.Button("ClearButton", buttons.transform, "Clear Objective", theme, () => onClear?.Invoke());
            UiFactory.PreferredHeight(clear.gameObject, 30f);

            _unitsHeader = UiFactory.Label("UnitsHeader", layout.transform, "Units", 14f, theme.Text);
            UiFactory.PreferredHeight(_unitsHeader.gameObject, 22f);

            _unitsContainer = UiFactory.VerticalLayout("Units", layout.transform, 2f, new RectOffset(0, 0, 0, 0)).transform;
        }

        public void SetVisible(bool visible)
        {
            if (_root != null)
            {
                _root.gameObject.SetActive(visible);
            }
        }

        public void Render(CommanderState state)
        {
            if (_root == null || state == null)
            {
                return;
            }

            _title.text = state.HasLocalFaction ? $"COMMANDER — {state.Faction.Name}" : "COMMANDER";
            _status.text = state.StatusLine;
            _placeButton.interactable = state.HasLocalFaction;

            var snap = state.Assignments;
            _unitsHeader.text = $"Assigned units: {snap.Total}  ({snap.CommandableCount} commandable)";

            EnsureRows(snap.Units.Count);
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (i < snap.Units.Count)
                {
                    var u = snap.Units[i];
                    bool arrived = u.State == AssignmentState.Arrived;
                    string tag = arrived ? "ARRIVED" : "en route";
                    row.text = $"{u.UnitName}  ·  {Mathf.RoundToInt(u.DistanceToObjective)} m  ·  {tag}";
                    row.color = arrived ? _theme.Arrived : _theme.EnRoute;
                    row.gameObject.SetActive(true);
                }
                else
                {
                    row.gameObject.SetActive(false);
                }
            }
        }

        private void EnsureRows(int count)
        {
            while (_rows.Count < count)
            {
                var row = UiFactory.Label("UnitRow" + _rows.Count, _unitsContainer, "", 12f, _theme.Text);
                UiFactory.PreferredHeight(row.gameObject, 16f);
                _rows.Add(row);
            }
        }
    }
}
