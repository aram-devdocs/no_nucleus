using System;
using System.Collections.Generic;

namespace CommanderLayer.Abstractions
{
    /// <summary>
    /// The registration entry point. The host installs <see cref="Register"/>; each mod calls it from its
    /// BepInEx Awake (after a hard dependency on the platform guarantees the host loaded first). Explicit
    /// registration — not a reflection scan — so load order is deterministic. While the platform and Commander
    /// ship as one assembly (Phase 3), the host simply sets the handler in-process.
    /// </summary>
    public static class ModPlatform
    {
        /// <summary>BepInPlugin GUID of the host (used by mods' [BepInDependency] once they are separate plugins).</summary>
        public const string Guid = "com.nucleus.platform";

        private static Action<IMod> _register;
        private static readonly List<IMod> _pending = new List<IMod>();

        /// <summary>Called by the host to install the registration handler; flushes any mods that registered
        /// before the host was ready.</summary>
        public static void SetHandler(Action<IMod> handler)
        {
            _register = handler ?? throw new ArgumentNullException(nameof(handler));
            foreach (var m in _pending) _register(m);
            _pending.Clear();
        }

        /// <summary>Called by a mod to register itself with the host. Buffered until the host installs a handler.</summary>
        public static void Register(IMod mod)
        {
            if (mod == null) throw new ArgumentNullException(nameof(mod));
            if (_register != null) _register(mod);
            else _pending.Add(mod);
        }
    }
}
