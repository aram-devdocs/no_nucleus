using System;

namespace CommanderLayer.Core
{
    /// <summary>
    /// Tiny logging seam so the pure/SDK libraries can emit log lines without referencing the BepInEx
    /// plugin (a lib must never depend on the host app). The host wires these sinks to its
    /// ManualLogSource at startup; the defaults are no-ops (e.g. under unit tests). Pure: just delegates.
    /// </summary>
    public static class NucleusLog
    {
        public static Action<string> Info = _ => { };
        public static Action<string> Warn = _ => { };
        public static Action<string> Error = _ => { };
    }
}
