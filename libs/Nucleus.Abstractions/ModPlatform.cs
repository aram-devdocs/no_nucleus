using System;
using System.Collections.Generic;

namespace Nucleus.Abstractions
{
    /// <summary>The registration entry point. The host installs a handler via <see cref="SetHandler"/>; each mod
    /// calls <see cref="Register"/> from its BepInEx Awake. Explicit registration, not a reflection scan, so load
    /// order is deterministic.</summary>
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
