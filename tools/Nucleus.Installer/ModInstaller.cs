using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Nucleus.Installer
{
    /// <summary>The outcome of an install/uninstall: what was done, where, and any errors.</summary>
    public sealed class InstallResult
    {
        public string PluginsDir;
        public bool DryRun;
        public readonly List<string> Installed = new List<string>();
        public readonly List<string> Removed = new List<string>();
        public readonly List<string> Errors = new List<string>();
        public bool Ok => Errors.Count == 0;
    }

    /// <summary>
    /// Deploys the Nucleus mod plugin folders into a player's Nuclear Option install. Pure framework IO and
    /// fully bounded: it only ever touches <c>&lt;game&gt;/BepInEx/plugins/Nucleus.*</c> — never anything else
    /// under the game, never outside the plugins folder. Requires BepInEx to already be present (it does not
    /// download or install BepInEx — that is a documented prerequisite). Testable headlessly with temp dirs.
    /// </summary>
    public static class ModInstaller
    {
        public const string PluginPrefix = "Nucleus.";

        /// <summary>Copy every <c>Nucleus.*</c> plugin folder from <paramref name="sourceDir"/> into the game's
        /// BepInEx plugins folder. <paramref name="dryRun"/> reports what would happen without writing.</summary>
        public static InstallResult Install(string sourceDir, string gameDir, bool dryRun = false)
        {
            var r = new InstallResult { DryRun = dryRun };

            if (!Directory.Exists(sourceDir)) { r.Errors.Add($"Source not found: {sourceDir}"); return r; }
            if (!Directory.Exists(gameDir)) { r.Errors.Add($"Game folder not found: {gameDir}"); return r; }

            var bepinex = Path.Combine(gameDir, "BepInEx");
            if (!Directory.Exists(bepinex))
            {
                r.Errors.Add($"BepInEx not found at {bepinex}. Install BepInEx 5 (x64, Mono) first, then re-run.");
                return r;
            }

            var pluginsDir = Path.Combine(bepinex, "plugins");
            r.PluginsDir = pluginsDir;

            var folders = Directory.GetDirectories(sourceDir)
                .Where(d => Path.GetFileName(d).StartsWith(PluginPrefix, StringComparison.Ordinal))
                .OrderBy(d => d)
                .ToList();
            if (folders.Count == 0) { r.Errors.Add($"No {PluginPrefix}* plugin folders under {sourceDir}"); return r; }

            if (!dryRun) Directory.CreateDirectory(pluginsDir);
            foreach (var folder in folders)
            {
                var name = Path.GetFileName(folder);
                var dest = Path.Combine(pluginsDir, name);
                try
                {
                    if (!dryRun) CopyDir(folder, dest);
                    r.Installed.Add(name);
                }
                catch (Exception ex) { r.Errors.Add($"{name}: {ex.Message}"); }
            }
            return r;
        }

        /// <summary>Remove every installed <c>Nucleus.*</c> plugin folder from the game's BepInEx plugins.</summary>
        public static InstallResult Uninstall(string gameDir, bool dryRun = false)
        {
            var r = new InstallResult { DryRun = dryRun };
            var pluginsDir = Path.Combine(gameDir, "BepInEx", "plugins");
            r.PluginsDir = pluginsDir;
            if (!Directory.Exists(pluginsDir)) { r.Errors.Add($"Plugins folder not found: {pluginsDir}"); return r; }

            foreach (var dir in Directory.GetDirectories(pluginsDir)
                         .Where(d => Path.GetFileName(d).StartsWith(PluginPrefix, StringComparison.Ordinal))
                         .OrderBy(d => d))
            {
                var name = Path.GetFileName(dir);
                try
                {
                    if (!dryRun) Directory.Delete(dir, recursive: true);
                    r.Removed.Add(name);
                }
                catch (Exception ex) { r.Errors.Add($"{name}: {ex.Message}"); }
            }
            return r;
        }

        private static void CopyDir(string source, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
            foreach (var sub in Directory.GetDirectories(source))
                CopyDir(sub, Path.Combine(dest, Path.GetFileName(sub)));
        }
    }
}
