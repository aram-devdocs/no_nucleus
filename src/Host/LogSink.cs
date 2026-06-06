using BepInEx.Logging;
using CommanderLayer.Abstractions;

namespace CommanderLayer.Host
{
    /// <summary>Adapts the host's BepInEx ManualLogSource to the mod-facing <see cref="ILogSink"/>.</summary>
    public sealed class LogSink : ILogSink
    {
        private readonly ManualLogSource _log;
        public LogSink(ManualLogSource log) { _log = log; }
        public void Info(string message) => _log?.LogInfo(message);
        public void Warn(string message) => _log?.LogWarning(message);
        public void Error(string message) => _log?.LogError(message);
    }
}
