using Nucleus.Abstractions;
using Nucleus.Ui;
using UnityEngine;

namespace Nucleus.Squad
{
    /// <summary>
    /// Squad assembly + command: the SQUADS slice of the shared campaign. Renders the squad list (name /
    /// family / strength / activity) with a per-squad AUTO/MANUAL toggle into its own native MFD screen;
    /// toggling routes through the shared campaign so the brain yields that squad to the player.
    /// </summary>
    public sealed class SquadMod : IMod
    {
        private IModContext _ctx;
        private CommanderPanel _panel;

        public ModInfo Info { get; } = new ModInfo
        {
            Id = "squad",
            DisplayName = "Squad",
            Version = "0.1.0",
            Author = "Nucleus",
            Description = "Assemble squads from your forces and command them.",
        };

        public void Initialize(IModContext ctx)
        {
            _ctx = ctx;
            ctx.Log.Info("[NUCLEUS:SELFTEST] PASS squad-mod-loaded");
            ctx.Log.Info($"[NUCLEUS:METRIC] squadRoster={ctx.Game.Roster().Count}");

            ctx.Buttons.RegisterMapButton(new MapButtonSpec
            {
                ModId = Info.Id,
                Label = "SQD",
                BuildContent = parent =>
                {
                    _panel = new CommanderPanel(parent, ctx.Ui.Theme,
                        onToggleSquadManual: id => ctx.Campaign?.ToggleSquadManual(id),
                        sections: CommanderPanel.PanelSections.Squads);
                    UiFactory.Stretch(_panel.Root);
                },
            });
        }

        public void Tick(IModTickContext t)
        {
            var c = _ctx?.Campaign;
            if (_panel != null && c != null) _panel.RenderHq(c.Hq(), c.Catalog(), c.Funds());
        }

        public void OnEnabled() { }
        public void OnDisabled() { }
        public void Shutdown() { }
    }
}
