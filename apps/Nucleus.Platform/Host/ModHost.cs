using BepInEx.Logging;
using Nucleus.Abstractions;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Nucleus.Host
{
    /// <summary>In-process host: owns the mod registry + shared game services and drives the per-frame tick over
    /// enabled mods. The DynamicMap.Update Harmony postfix calls <see cref="Tick"/>.</summary>
    public sealed class ModHost
    {
        private readonly ModRegistry _registry;
        private readonly GameServices _game = new GameServices();
        private readonly HostButtons _buttons = new HostButtons();
        private readonly LogSink _log;
        // The shared live campaign, published once by the Commander mod and read by Build/Squad/Warfare.
        private Nucleus.Core.Command.ICampaign _campaign;

        /// <summary>The shared live campaign (null until the Commander mod publishes it). For the dev harness.</summary>
        public Nucleus.Core.Command.ICampaign Campaign => _campaign;

        /// <summary>The shared game-services surface (roster/intel/join/census). For the setup controller + harness.</summary>
        public Nucleus.Abstractions.IGameServices Game => _game;

        public ModHost(ManualLogSource log,
            System.Func<string, bool> readEnabled = null,
            System.Action<string, bool> writeEnabled = null)
        {
            _log = new LogSink(log);
            var read = readEnabled ?? (_ => true);
            // persist (writeEnabled) saves a runtime toggle; read seeds each mod's initial enabled state.
            _registry = new ModRegistry(mod => new ModContext(mod, _log, _game, _buttons,
                () => _campaign, c => _campaign = c), writeEnabled);
            // Install the registration handler so mods (this assembly + separate plugins) resolve through the
            // host. Mods that registered before the host was ready are flushed by SetHandler.
            ModPlatform.SetHandler(m => _registry.Add(m, read(m.Info.Id)));
        }

        public ModRegistry Registry => _registry;
        private bool _selfTested;

        /// <summary>Called by the VirtualMFD patch (after the runtime's CMD attach): attach each mod's
        /// registered bezel button to a distinct remaining blank slot.</summary>
        public void AttachButtons(VirtualMFD mfd) => _buttons.AttachTo(mfd);

        /// <summary>Per-frame pump (from the DynamicMap.Update postfix): tick every enabled mod.</summary>
        public void Tick()
        {
            _registry.TickAll(new TickContext(
                mapOpen: DynamicMap.mapMaximized,
                dt: Time.unscaledDeltaTime,
                pointerOverUi: EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()));

            // Keep bezel button "open" tints in sync with their screens' actual state (fixes the stale-open desync).
            _buttons.RefreshTints();

            if (!_selfTested) TrySelfTest();
        }

        // One-shot machine-readable self-test (once a mission's roster is readable) that tools/Nucleus.LogAudit
        // verifies automatically; its absence in a future log is itself the regression signal.
        private void TrySelfTest()
        {
            System.Collections.Generic.IReadOnlyList<Core.Model.UnitView> roster;
            try { roster = _game.Roster(); } catch { return; }   // game not ready yet
            if (roster == null || roster.Count == 0) return;       // wait for a loaded mission with units
            _selfTested = true;

            _log.Info($"[NUCLEUS:METRIC] mods={_registry.Count}");
            _log.Info($"[NUCLEUS:METRIC] roster={roster.Count}");
            _log.Info("[NUCLEUS:SELFTEST] PASS host-tick-alive");
            _log.Info(_registry.Count > 0
                ? "[NUCLEUS:SELFTEST] PASS mods-registered"
                : "[NUCLEUS:SELFTEST] FAIL mods-registered");
            _log.Info("[NUCLEUS:SELFTEST] PASS game-services-readable");
        }

        private sealed class TickContext : IModTickContext
        {
            public TickContext(bool mapOpen, float dt, bool pointerOverUi)
            {
                MapOpen = mapOpen;
                UnscaledDeltaTime = dt;
                PointerOverModUi = pointerOverUi;
            }
            public bool MapOpen { get; }
            public float UnscaledDeltaTime { get; }
            public bool PointerOverModUi { get; }
        }
    }
}
