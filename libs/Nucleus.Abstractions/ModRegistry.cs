using System;
using System.Collections.Generic;

namespace Nucleus.Abstractions
{
    /// <summary>
    /// Holds the registered mods + their enabled state and drives the host-owned lifecycle (initialize on
    /// first enable, tick while enabled, OnEnabled/OnDisabled on runtime toggle). The host supplies a context
    /// factory so each mod gets its <see cref="IModContext"/> lazily at initialize time.
    /// </summary>
    public sealed class ModRegistry
    {
        private sealed class Entry
        {
            public IMod Mod;
            public bool Enabled;
            public bool Initialized;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly Func<IMod, IModContext> _contextFor;
        private readonly Action<string, bool>? _persist;

        /// <param name="contextFor">Factory that builds a mod's context at initialize time.</param>
        /// <param name="persist">Optional: called with (modId, enabled) whenever a mod is toggled, so the host
        /// can save the choice (e.g. to BepInEx config). No-op by default.</param>
        public ModRegistry(Func<IMod, IModContext> contextFor, Action<string, bool>? persist = null)
        {
            _contextFor = contextFor ?? throw new ArgumentNullException(nameof(contextFor));
            _persist = persist;
        }

        public IEnumerable<IMod> Mods
        {
            get { foreach (var e in _entries) yield return e.Mod; }
        }

        public int Count => _entries.Count;

        /// <summary>Register a mod with an initial enabled state. Ignores a duplicate Id.</summary>
        public void Add(IMod mod, bool enabled)
        {
            if (mod == null) throw new ArgumentNullException(nameof(mod));
            // Fail fast with an actionable contract error rather than a bare NRE deep in Find/lifecycle.
            if (mod.Info == null) throw new ArgumentException("mod.Info must be set", nameof(mod));
            if (string.IsNullOrEmpty(mod.Info.Id)) throw new ArgumentException("mod.Info.Id must be set", nameof(mod));
            if (Find(mod.Info.Id) != null) return;
            _entries.Add(new Entry { Mod = mod, Enabled = enabled });
            if (enabled) EnsureInitialized(_entries[_entries.Count - 1]);
        }

        public bool IsEnabled(string id) => Find(id)?.Enabled ?? false;

        /// <summary>Tick every enabled (initialized) mod, in registration order.</summary>
        public void TickAll(IModTickContext t)
        {
            foreach (var e in _entries)
                if (e.Enabled && e.Initialized) e.Mod.Tick(t);
        }

        /// <summary>Toggle a mod at runtime: initialize on first enable, fire OnEnabled/OnDisabled.</summary>
        public void SetEnabled(string id, bool on)
        {
            var e = Find(id);
            if (e == null || e.Enabled == on) return;
            e.Enabled = on;
            if (on) { EnsureInitialized(e); e.Mod.OnEnabled(); }
            else e.Mod.OnDisabled();
            _persist?.Invoke(id, on);
        }

        public void ShutdownAll()
        {
            foreach (var e in _entries) e.Mod.Shutdown();
        }

        private void EnsureInitialized(Entry e)
        {
            if (e.Initialized) return;
            e.Mod.Initialize(_contextFor(e.Mod));
            e.Initialized = true;
        }

        private Entry Find(string id)
        {
            foreach (var e in _entries)
                if (e.Mod.Info.Id == id) return e;
            return null;
        }
    }
}
