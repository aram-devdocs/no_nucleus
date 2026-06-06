using Nucleus.Abstractions;
using Nucleus.Core.Command;
using Nucleus.Core.Persistence;

namespace Nucleus.Warfare
{
    /// <summary>
    /// Nucleus Dynamic Warfare: a persistent two-faction war where both sides run the autonomous commander.
    /// Owns the <see cref="WarfareCampaign"/> and its save/resume to disk (resumes any existing save on load).
    /// The headless substrate — dual-faction stepping + lossless save/resume + continuation determinism — is
    /// proven in Nucleus.Sim.Tests; this mod drives it in-game. The per-faction battlefield views come from the
    /// "Nucleus Dynamic Warfare" mission (which grants both sides' rosters); until that mission runs, the WAR
    /// button reports campaign status and the campaign persists across sessions.
    /// </summary>
    public sealed class WarfareMod : IMod
    {
        private readonly string _savePath;
        private WarfareCampaign _campaign;
        private IModContext _ctx;

        public WarfareMod(string savePath) { _savePath = savePath; }

        public ModInfo Info { get; } = new ModInfo
        {
            Id = "warfare",
            DisplayName = "Warfare",
            Version = "0.1.0",
            Author = "Nucleus",
            Description = "Persistent two-faction dynamic war (both sides run the AI commander); save and resume.",
        };

        public void Initialize(IModContext ctx)
        {
            _ctx = ctx;
            _campaign = WarfareSave.Load(_savePath) ?? new WarfareCampaign();

            ctx.Log.Info("[NUCLEUS:SELFTEST] PASS warfare-mod-loaded");
            ctx.Log.Info($"[NUCLEUS:METRIC] warfareTurn={_campaign.Turn}");

            ctx.Buttons.RegisterMapButton(new MapButtonSpec
            {
                ModId = Info.Id,
                Label = "WAR",
                OnClick = ReportStatus,
            });
        }

        /// <summary>The live campaign (for the mission driver / future Warfare panel).</summary>
        public WarfareCampaign Campaign => _campaign;

        /// <summary>Persist the current war so it can be resumed next session.</summary>
        public void Save() => WarfareSave.Save(_savePath, _campaign);

        private void ReportStatus()
        {
            _ctx?.Log.Info($"[Warfare] turn {_campaign.Turn} — Blufor: {_campaign.Blufor.Objectives.Count} obj / "
                + $"{_campaign.Blufor.Operations.Count} ops · Opfor: {_campaign.Opfor.Objectives.Count} obj / "
                + $"{_campaign.Opfor.Operations.Count} ops");
        }

        public void Tick(IModTickContext t) { }
        public void OnEnabled() { }
        public void OnDisabled() { }
        public void Shutdown()
        {
            // Persist on shutdown so a multi-hour war survives quitting the game.
            if (_campaign != null && _campaign.Turn > 0) Save();
        }
    }
}
