using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using BepInEx.Logging;

namespace Nucleus.Host
{
    /// <summary>Best-effort, fail-silent check against the latest GitHub release. Runs on a background thread (no
    /// Unity networking module needed), logs a prominent nudge, and sets a flag the UI can surface when a newer
    /// Nucleus version is available. Never blocks load; offline/rate-limited = silent. Opt-out via config.</summary>
    internal static class UpdateChecker
    {
        private const string Api = "https://api.github.com/repos/aram-devdocs/no_nucleus/releases/latest";

        /// <summary>True once a newer release than the running build has been seen (for a menu badge to read).</summary>
        public static bool UpdateAvailable { get; private set; }
        public static string LatestTag { get; private set; }

        public static void CheckAsync(string currentVersion, ManualLogSource log)
        {
            var t = new Thread(() =>
            {
                try
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    var req = (HttpWebRequest)WebRequest.Create(Api);
                    req.UserAgent = "Nucleus";
                    req.Timeout = 8000;
                    using var resp = req.GetResponse();
                    using var sr = new StreamReader(resp.GetResponseStream());
                    var m = Regex.Match(sr.ReadToEnd(), "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                    if (!m.Success) return;
                    var tag = m.Groups[1].Value;
                    if (IsNewer(tag, currentVersion))
                    {
                        UpdateAvailable = true;
                        LatestTag = tag;
                        log.LogWarning($"[NUCLEUS] Update available: {tag} (installed {currentVersion}). " +
                                       "Run the Nucleus updater (Nucleus.Installer update) to upgrade.");
                    }
                    else log.LogInfo($"[NUCLEUS] Up to date ({currentVersion}).");
                }
                catch { /* offline / rate-limited / TLS — stay silent, never disrupt load */ }
            })
            { IsBackground = true, Name = "Nucleus.UpdateCheck" };
            t.Start();
        }

        private static bool IsNewer(string tag, string current)
        {
            Version P(string s) => Version.TryParse((s ?? "0").TrimStart('v', 'V'), out var v) ? v : new Version(0, 0, 0);
            return P(tag) > P(current);
        }
    }
}
