using UnityEngine.Events;
using UnityEngine.UI;

namespace Nucleus.Host
{
    /// <summary>
    /// Helpers for repurposing cloned native buttons. A clone keeps the original's PERSISTENT (editor-wired)
    /// onClick listeners, which <c>RemoveAllListeners()</c> does NOT clear — so a cloned bezel/menu button
    /// would still fire the template's action. <see cref="Rewire"/> disables the persistent calls and installs
    /// ours, so the clone does exactly one thing: what we want.
    /// </summary>
    internal static class NativeButtons
    {
        public static void Rewire(Button btn, UnityAction action)
        {
            if (btn == null) return;
            var e = btn.onClick;
            for (int i = e.GetPersistentEventCount() - 1; i >= 0; i--)
                e.SetPersistentListenerState(i, UnityEventCallState.Off);
            e.RemoveAllListeners();
            if (action != null) e.AddListener(action);
        }
    }
}
