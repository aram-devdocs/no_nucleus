using CommanderLayer.Abstractions;
using CommanderLayer.Ui;
using UnityEngine;

namespace CommanderLayer.Host
{
    /// <summary>
    /// The per-mod context the host hands a mod at initialize time. Phase 3: Log + the shared Game services
    /// are real; the UI surface and button registry are placeholders until the host owns the single Canvas and
    /// arbitrates bezel slots (Phase 4, when a second mod needs to share). Config binding returns the default
    /// for now (Commander reads the plugin config directly).
    /// </summary>
    internal sealed class ModContext : IModContext
    {
        private readonly IMod _mod;

        public ModContext(IMod mod, ILogSink log, IGameServices game, IButtonRegistry buttons)
        {
            _mod = mod;
            Log = log;
            Game = game;
            Buttons = buttons;
        }

        public ModInfo Info => _mod.Info;
        public bool IsEnabled => true;
        public ILogSink Log { get; }
        public IModUi Ui { get; } = new HostModUi();
        public IGameServices Game { get; }
        public IButtonRegistry Buttons { get; }   // host-owned, shared across mods (HostButtons)
        public T BindConfig<T>(string section, string key, T def, string description) => def;
    }

    /// <summary>Placeholder UI surface (a host-owned canvas layer is a later step). Commander uses its own
    /// runtime/canvas; Build/Squad reach the game via their bezel button + Game services for now.</summary>
    internal sealed class HostModUi : IModUi
    {
        public RectTransform CreateLayer(string name) => null;
        public Transform MapIconLayer => null;
        public Theme Theme { get; } = Theme.Default;
    }
}
