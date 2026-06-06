using BepInEx.Logging;
using CommanderLayer.Abstractions;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CommanderLayer.Host
{
    /// <summary>
    /// In-process host (single plugin, Phase 3): owns the mod registry + the shared game services and drives
    /// the per-frame tick over enabled mods. The DynamicMap.Update Harmony postfix calls <see cref="Tick"/>.
    /// Canvas/bezel-button/loader ownership is introduced as Build/Squad arrive (Phase 4-5); for now Commander
    /// keeps its own runtime and the host adds only the registry/tick layer.
    /// </summary>
    public sealed class ModHost
    {
        private readonly ModRegistry _registry;
        private readonly GameServices _game = new GameServices();
        private readonly HostButtons _buttons = new HostButtons();
        private readonly LogSink _log;

        public ModHost(ManualLogSource log)
        {
            _log = new LogSink(log);
            _registry = new ModRegistry(mod => new ModContext(mod, _log, _game, _buttons));
            // Install the registration handler so mods (this assembly, and separate plugins in Phase 4+)
            // resolve through the host. Mods that registered earlier are flushed by SetHandler.
            ModPlatform.SetHandler(m => _registry.Add(m, enabled: true));
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

            if (!_selfTested) TrySelfTest();
        }

        // One-shot structured self-test, emitted once the mission is live (roster readable). The lines are
        // machine-readable ([NUCLEUS:METRIC]/[NUCLEUS:SELFTEST]) so tools/Nucleus.LogAudit verifies a playtest
        // automatically instead of by hand. Their PRESENCE proves the host tick reached this point; their
        // ABSENCE in a future log is itself the regression signal.
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
