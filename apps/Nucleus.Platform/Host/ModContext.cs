using Nucleus.Abstractions;
using Nucleus.Ui;
using UnityEngine;

namespace Nucleus.Host
{
    /// <summary>The per-mod context the host hands a mod at initialize time: Log, shared Game services, and the
    /// host-owned button registry. The UI surface is a placeholder (Commander uses its own canvas) and
    /// BindConfig returns the default (Commander reads the plugin config directly).</summary>
    internal sealed class ModContext : IModContext
    {
        private readonly IMod _mod;
        private readonly System.Func<Nucleus.Core.Command.ICampaign> _getCampaign;
        private readonly System.Action<Nucleus.Core.Command.ICampaign> _setCampaign;

        public ModContext(IMod mod, ILogSink log, IGameServices game, IButtonRegistry buttons,
            System.Func<Nucleus.Core.Command.ICampaign> getCampaign,
            System.Action<Nucleus.Core.Command.ICampaign> setCampaign)
        {
            _mod = mod;
            Log = log;
            Game = game;
            Buttons = buttons;
            _getCampaign = getCampaign;
            _setCampaign = setCampaign;
        }

        public ModInfo Info => _mod.Info;
        public bool IsEnabled => true;
        public ILogSink Log { get; }
        public IModUi Ui { get; } = new HostModUi();
        public IGameServices Game { get; }
        public IButtonRegistry Buttons { get; }   // host-owned, shared across mods (HostButtons)
        public T BindConfig<T>(string section, string key, T def, string description) => def;

        // Shared live campaign: the Commander publishes it once; every mod reads it (host-owned holder).
        public Nucleus.Core.Command.ICampaign Campaign => _getCampaign?.Invoke();
        public void ShareCampaign(Nucleus.Core.Command.ICampaign campaign) => _setCampaign?.Invoke(campaign);
    }

    /// <summary>Minimal UI surface: each mod builds into its bezel screen and reaches the game via Game services.
    /// Commander owns its own runtime/canvas.</summary>
    internal sealed class HostModUi : IModUi
    {
        public RectTransform CreateLayer(string name) => null;
        public Transform MapIconLayer => null;
        public Theme Theme { get; } = Theme.Default;
    }
}
