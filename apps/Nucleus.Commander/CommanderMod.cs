using CommanderLayer.Abstractions;
using CommanderLayer.Composition;

namespace CommanderLayer.Commander
{
    /// <summary>
    /// Commander as a hosted mod: a thin wrapper over CommanderRuntime. Registers the CMD bezel button (the
    /// host attaches it to a blank MFD slot) which toggles the Commander panel; the runtime owns its own
    /// overlay canvas/screen.
    /// </summary>
    public sealed class CommanderMod : IMod
    {
        private readonly CommanderRuntime _runtime;

        public CommanderMod(CommanderRuntime runtime) { _runtime = runtime; }

        public ModInfo Info { get; } = new ModInfo
        {
            Id = "commander",
            DisplayName = "Commander",
            Version = CommanderPlugin.Version,
            Author = "Nucleus",
            Description = "Autonomous theater commander + manual map orders.",
        };

        public void Initialize(IModContext ctx)
        {
            // Claim the CMD bezel button; the host attaches it to a blank MFD slot when the map opens.
            ctx.Buttons.RegisterMapButton(new MapButtonSpec
            {
                ModId = Info.Id,
                Label = "CMD",
                OnClick = () => _runtime.ToggleScreen(),
            });
        }

        public void Tick(IModTickContext t) => _runtime.Tick();
        public void OnEnabled() { }
        public void OnDisabled() { }
        public void Shutdown() { }
    }
}
