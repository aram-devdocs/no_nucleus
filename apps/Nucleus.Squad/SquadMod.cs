using CommanderLayer.Abstractions;

namespace Nucleus.Squad
{
    /// <summary>
    /// Squad assembly + command: put together squads from your forces and command them — the bridge between
    /// Build (what you buy) and Commander (how it fights). First milestone proves the separate plugin loads,
    /// registers, and reads the shared roster across the plugin boundary. The squad UI lands once the host
    /// exposes real UI/button services.
    /// </summary>
    public sealed class SquadMod : IMod
    {
        private IModContext _ctx;

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

            // Claim a SQD bezel button (host attaches it to a blank slot). Squad panel lands with host UI.
            ctx.Buttons.RegisterMapButton(new MapButtonSpec
            {
                ModId = Info.Id,
                Label = "SQD",
                OnClick = () => ctx.Log.Info("[Squad] squad manager — coming soon"),
            });
        }

        public void Tick(IModTickContext t) { }
        public void OnEnabled() { }
        public void OnDisabled() { }
        public void Shutdown() { }
    }
}
