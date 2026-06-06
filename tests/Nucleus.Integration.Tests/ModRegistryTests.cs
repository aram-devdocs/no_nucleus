using System.Collections.Generic;
using CommanderLayer.Abstractions;
using Xunit;

namespace Nucleus.Integration.Tests
{
    /// <summary>
    /// Headless host-lifecycle tests: drives the real ModRegistry with FakeMods (no Unity runtime calls).
    /// Verifies initialize-on-enable, tick-only-enabled, toggle callbacks, init-once, and duplicate-id guard —
    /// the platform's mod management, provable without the game.
    /// </summary>
    public class ModRegistryTests
    {
        private static ModRegistry New() => new ModRegistry(_ => null);

        [Fact]
        public void Enabled_mod_is_initialized_on_add()
        {
            var m = new FakeMod("a");
            var r = New();
            r.Add(m, enabled: true);
            Assert.Equal(1, m.Inits);
        }

        [Fact]
        public void Disabled_mod_initializes_only_when_enabled()
        {
            var m = new FakeMod("a");
            var r = New();
            r.Add(m, enabled: false);
            Assert.Equal(0, m.Inits);
            r.SetEnabled("a", true);
            Assert.Equal(1, m.Inits);
            Assert.Equal(1, m.Enables);
        }

        [Fact]
        public void TickAll_ticks_only_enabled_mods()
        {
            var a = new FakeMod("a");
            var b = new FakeMod("b");
            var r = New();
            r.Add(a, enabled: true);
            r.Add(b, enabled: false);
            r.TickAll(null);
            Assert.Equal(1, a.Ticks);
            Assert.Equal(0, b.Ticks);
        }

        [Fact]
        public void Disable_then_enable_fires_callbacks_and_gates_ticks()
        {
            var m = new FakeMod("a");
            var r = New();
            r.Add(m, enabled: true);

            r.SetEnabled("a", false);
            Assert.Equal(1, m.Disables);
            r.TickAll(null);
            Assert.Equal(0, m.Ticks);

            r.SetEnabled("a", true);
            Assert.Equal(1, m.Enables);
            r.TickAll(null);
            Assert.Equal(1, m.Ticks);
        }

        [Fact]
        public void Mod_is_initialized_at_most_once()
        {
            var m = new FakeMod("a");
            var r = New();
            r.Add(m, enabled: true);
            r.SetEnabled("a", false);
            r.SetEnabled("a", true);
            Assert.Equal(1, m.Inits);
        }

        [Fact]
        public void Duplicate_id_is_ignored()
        {
            var r = New();
            r.Add(new FakeMod("a"), enabled: true);
            r.Add(new FakeMod("a"), enabled: true);
            Assert.Equal(1, r.Count);
        }

        [Fact]
        public void SetEnabled_invokes_the_persist_callback()
        {
            var saved = new List<(string id, bool on)>();
            var r = new ModRegistry(_ => null, (id, on) => saved.Add((id, on)));
            r.Add(new FakeMod("a"), enabled: true);
            r.SetEnabled("a", false);
            r.SetEnabled("a", true);
            Assert.Equal(2, saved.Count);
            Assert.Equal(("a", false), saved[0]);
            Assert.Equal(("a", true), saved[1]);
        }

        [Fact]
        public void ShutdownAll_shuts_down_every_mod()
        {
            var a = new FakeMod("a");
            var b = new FakeMod("b");
            var r = New();
            r.Add(a, enabled: true);
            r.Add(b, enabled: false);
            r.ShutdownAll();
            Assert.Equal(1, a.Shutdowns);
            Assert.Equal(1, b.Shutdowns);
        }
    }
}
