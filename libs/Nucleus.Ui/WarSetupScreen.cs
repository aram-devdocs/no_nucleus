using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nucleus.Ui
{
    /// <summary>
    /// The pre-mission setup screen for Nucleus Dynamic Warfare, built from the shared <see cref="ModPanel"/>
    /// chrome so it reads as a native game window. Lets the player pick a SIDE, set each side's commander to
    /// YOU or AI, toggle AI AUTO-FILL, and START the war. Logic-free: it owns its selection state and reports
    /// the choices through <c>onStart</c>; the host applies them (join + war config). No game access.
    /// </summary>
    public sealed class WarSetupScreen
    {
        private readonly Theme _theme;
        private readonly ModPanel _panel;
        private readonly List<string> _factions;
        private readonly Action<string, bool, bool> _onStart; // (playerFaction, aiCommander, aiAutoFill)

        private string _playerFaction;
        private bool _aiCommander = true;   // AI creates your side's objectives (default on)
        private bool _aiAutoFill = true;    // AI forms/recruits/assigns squads (default on)

        private readonly List<(string faction, Image img, TextMeshProUGUI label)> _sideRows
            = new List<(string, Image, TextMeshProUGUI)>();
        private Image _aiCmdImg, _autoFillImg;
        private TextMeshProUGUI _aiCmdLabel, _autoFillLabel;

        private static readonly Color OnColor = new Color(0.30f, 0.85f, 0.45f, 1f);

        public RectTransform Root => _panel.Root;

        public WarSetupScreen(Transform parent, Theme theme, IReadOnlyList<string> factions,
            Action<string, bool, bool> onStart)
        {
            _theme = theme;
            _onStart = onStart;
            _factions = new List<string>(factions ?? new List<string>());
            if (_factions.Count > 0) _playerFaction = _factions[0];

            _panel = new ModPanel(parent, theme, "NUCLEUS DYNAMIC WARFARE");
            // Center the setup panel (override ModPanel's default top-left anchor).
            Root.anchorMin = Root.anchorMax = Root.pivot = new Vector2(0.5f, 0.5f);
            Root.anchoredPosition = Vector2.zero;
            Root.sizeDelta = new Vector2(480f, 520f);

            var col = UiFactory.VerticalLayout("SetupCol", _panel.Content, 8f, new RectOffset(16, 16, 14, 16));
            UiFactory.Stretch((RectTransform)col.transform);

            UiFactory.PreferredHeight(UiFactory.Label("Hdr", col.transform,
                "Choose your side. The other sides are run by the AI commander. Attrition wins the war.",
                12f, theme.Muted).gameObject, 40f);

            UiFactory.PreferredHeight(UiFactory.Label("SideHdr", col.transform, "YOUR SIDE", 13f, theme.Accent).gameObject, 20f);
            foreach (var f in _factions)
            {
                var btn = UiFactory.Button("Side_" + f, col.transform, f, theme, null);
                UiFactory.PreferredHeight(btn.gameObject, 32f);
                var img = btn.GetComponent<Image>();
                var lbl = btn.GetComponentInChildren<TextMeshProUGUI>();
                string fac = f;
                btn.onClick.AddListener(() => { _playerFaction = fac; Refresh(); });
                _sideRows.Add((f, img, lbl));
            }

            UiFactory.PreferredHeight(UiFactory.Label("CmdHdr", col.transform, "COMMAND", 13f, theme.Accent).gameObject, 20f);
            var aiCmd = UiFactory.Button("AiCmd", col.transform, "AI COMMANDER", theme, () => { _aiCommander = !_aiCommander; Refresh(); });
            UiFactory.PreferredHeight(aiCmd.gameObject, 30f);
            _aiCmdImg = aiCmd.GetComponent<Image>(); _aiCmdLabel = aiCmd.GetComponentInChildren<TextMeshProUGUI>();

            var autoFill = UiFactory.Button("AutoFill", col.transform, "AI AUTO-FILL", theme, () => { _aiAutoFill = !_aiAutoFill; Refresh(); });
            UiFactory.PreferredHeight(autoFill.gameObject, 30f);
            _autoFillImg = autoFill.GetComponent<Image>(); _autoFillLabel = autoFill.GetComponentInChildren<TextMeshProUGUI>();

            UiFactory.PreferredHeight(UiFactory.Label("CmdHint", col.transform,
                "AI COMMANDER on = the AI creates your objectives (off = you drop them). AI AUTO-FILL on = the AI forms squads and assigns them.",
                11f, theme.Muted).gameObject, 44f);

            var start = UiFactory.Button("Start", col.transform, "START WAR", theme,
                () => _onStart?.Invoke(_playerFaction, _aiCommander, _aiAutoFill));
            UiFactory.PreferredHeight(start.gameObject, 38f);
            start.GetComponent<Image>().color = OnColor;

            Refresh();
        }

        private void Refresh()
        {
            foreach (var (faction, img, label) in _sideRows)
            {
                bool sel = faction == _playerFaction;
                img.color = sel ? OnColor : _theme.ButtonIdle;
                if (label != null) label.text = sel ? $"{faction}  [YOU]" : $"{faction}  (AI)";
            }
            if (_aiCmdImg != null) _aiCmdImg.color = _aiCommander ? OnColor : _theme.ButtonIdle;
            if (_autoFillImg != null) _autoFillImg.color = _aiAutoFill ? OnColor : _theme.ButtonIdle;
            if (_aiCmdLabel != null) _aiCmdLabel.text = _aiCommander ? "AI COMMANDER: ON" : "AI COMMANDER: OFF";
            if (_autoFillLabel != null) _autoFillLabel.text = _aiAutoFill ? "AI AUTO-FILL: ON" : "AI AUTO-FILL: OFF";
        }

        public void Destroy()
        {
            if (Root != null) UnityEngine.Object.Destroy(Root.gameObject);
        }
    }
}
