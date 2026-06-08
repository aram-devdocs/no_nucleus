using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Nucleus.Installer
{
    /// <summary>
    /// First-class installer for the Nucleus mod (Nuclear Option, Steam AppId 2168680). Windows-first.
    ///   detect                                   — find the game install
    ///   install   [--game auto|&lt;dir&gt;] [--source &lt;dir&gt;] [--no-shortcut] [--dry-run]
    ///   launch    [--game auto|&lt;dir&gt;]            — start the game WITH Nucleus (used by the desktop shortcut)
    ///   update    [--game auto|&lt;dir&gt;]            — upgrade the mod from the latest GitHub release if newer
    ///   verify    [--game auto|&lt;dir&gt;] [--source &lt;dir&gt;] — repair BepInEx/Doorstop/plugins
    ///   uninstall [--game auto|&lt;dir&gt;] [--dry-run]
    /// Vanilla unless you launch via the Nucleus shortcut: install leaves Doorstop disabled; `launch` flips it for
    /// the session only. Only ever adds files alongside the game — never edits game assemblies.
    /// </summary>
    public static class Program
    {
        private static void Log(string m) => Console.WriteLine(m);

        public static int Main(string[] args)
        {
            if (args.Length == 0) { Usage(); return 2; }
            var cmd = args[0].ToLowerInvariant();
            string source = Arg(args, "--source") ?? AppContext.BaseDirectory;
            bool dryRun = args.Any(a => a == "--dry-run");
            bool noShortcut = args.Any(a => a == "--no-shortcut");

            try
            {
                switch (cmd)
                {
                    case "detect": return Detect();
                    case "install": return Install(ResolveGame(args), source, dryRun, noShortcut);
                    case "launch": return Launcher.LaunchModded(ResolveGame(args), Log);
                    case "update": return Update(ResolveGame(args));
                    case "verify": return Verify(ResolveGame(args), source);
                    case "uninstall": return Uninstall(ResolveGame(args), dryRun);
                    default: Usage(); return 2;
                }
            }
            catch (InstallerError e) { Console.Error.WriteLine("ERROR: " + e.Message); return 2; }
        }

        private sealed class InstallerError : Exception { public InstallerError(string m) : base(m) { } }

        private static string ResolveGame(string[] args)
        {
            var g = Arg(args, "--game");
            if (string.IsNullOrEmpty(g) || g.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                g = GameLocator.Locate();
                if (g == null) throw new InstallerError("Could not auto-detect Nuclear Option. Pass --game \"<folder>\".");
                Log("Detected game: " + g);
            }
            if (!GameLocator.IsGameDir(g)) throw new InstallerError($"Not a Nuclear Option folder (no NuclearOption.exe): {g}");
            return Path.GetFullPath(g);
        }

        private static int Detect()
        {
            var g = GameLocator.Locate();
            if (g == null) { Console.Error.WriteLine("Nuclear Option not found via Steam."); return 1; }
            Log(g);
            return 0;
        }

        private static int Install(string game, string source, bool dryRun, bool noShortcut)
        {
            Log("Nucleus installer — EARLY ALPHA");
            if (!dryRun && !BepInExBootstrapper.EnsureInstalled(game, Log)) return 1;
            if (!dryRun) DoorstopToggle.SetEnabled(game, false); // vanilla on a plain Steam launch

            var r = ModInstaller.Install(source, game, dryRun);
            foreach (var n in r.Installed) Log($"  installed{(dryRun ? " (dry-run)" : "")}: {n}");
            foreach (var e in r.Errors) Console.Error.WriteLine("  ERROR: " + e);
            if (!r.Ok) { Console.Error.WriteLine("FAILED — see errors above."); return 1; }

            var version = VersionStamp.Shipping(source);
            if (!dryRun)
            {
                VersionStamp.Write(game, version);
                MissionInstaller.Install(source, Log);   // installer-only users get the mission too
                var (toolExe, iconPath) = PersistTools(game, source);
                if (!noShortcut)
                    ShortcutFactory.Create(toolExe, $"launch --game \"{game}\"", game, iconPath, Log);
            }
            Log($"OK{(dryRun ? " (dry-run)" : "")} — Nucleus {version} installed. Steam launch = vanilla; use the desktop shortcut for Nucleus.");
            return 0;
        }

        private static int Update(string game)
        {
            var installed = VersionStamp.ReadInstalled(game) ?? "0.0.0";
            var latest = GitHubReleases.Latest(GitHubReleases.NucleusRepo, @"Nucleus-.*\.zip$");
            if (latest?.Url == null) { Log($"No downloadable release found (installed {installed})."); return 0; }
            if (GitHubReleases.CompareVersions(latest.Tag, installed) <= 0)
            { Log($"Up to date (installed {installed}, latest {latest.Tag})."); return 0; }

            Log($"Updating {installed} -> {latest.Tag} ...");
            var tmpZip = Path.Combine(Path.GetTempPath(), "nucleus-update.zip");
            var tmpDir = Path.Combine(Path.GetTempPath(), "nucleus-update");
            if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
            GitHubReleases.Download(latest.Url, tmpZip);
            ZipFile.ExtractToDirectory(tmpZip, tmpDir, overwriteFiles: true);

            var src = ModInstaller.HasPlugins(tmpDir) ? tmpDir
                : Directory.GetDirectories(tmpDir).FirstOrDefault(ModInstaller.HasPlugins) ?? tmpDir;
            var r = ModInstaller.Install(src, game, dryRun: false);
            foreach (var e in r.Errors) Console.Error.WriteLine("  ERROR: " + e);
            if (!r.Ok) return 1;
            VersionStamp.Write(game, latest.Tag.TrimStart('v', 'V'));
            Log($"Updated to {latest.Tag}.");
            return 0;
        }

        private static int Verify(string game, string source)
        {
            if (!BepInExBootstrapper.EnsureInstalled(game, Log)) return 1;
            DoorstopToggle.SetEnabled(game, false);
            if (ModInstaller.HasPlugins(source))
            {
                var r = ModInstaller.Install(source, game, dryRun: false);
                foreach (var e in r.Errors) Console.Error.WriteLine("  ERROR: " + e);
                if (!r.Ok) return 1;
            }
            Log("Verify OK — BepInEx present, Doorstop disabled (vanilla on Steam launch), plugins in place.");
            return 0;
        }

        private static int Uninstall(string game, bool dryRun)
        {
            var r = ModInstaller.Uninstall(game, dryRun);
            foreach (var n in r.Removed) Log($"  removed{(dryRun ? " (dry-run)" : "")}: {n}");
            foreach (var e in r.Errors) Console.Error.WriteLine("  ERROR: " + e);
            if (!dryRun) ShortcutFactory.Remove(Log);
            Log(r.Ok ? "Uninstalled the Nucleus plugins (BepInEx left in place)." : "FAILED — see errors above.");
            return r.Ok ? 0 : 1;
        }

        // Copy the installer + icon into the game so the shortcut + future updates have a persistent tool.
        private static (string toolExe, string iconPath) PersistTools(string game, string source)
        {
            var tools = Path.Combine(game, "BepInEx", "nucleus");
            Directory.CreateDirectory(tools);
            var self = Environment.ProcessPath;
            var toolExe = Path.Combine(tools, "Nucleus.Installer.exe");
            try { if (self != null && File.Exists(self)) File.Copy(self, toolExe, true); } catch { toolExe = self; }
            var iconSrc = Path.Combine(source, "nucleus.ico");
            var iconPath = Path.Combine(tools, "nucleus.ico");
            try { if (File.Exists(iconSrc)) File.Copy(iconSrc, iconPath, true); else iconPath = null; } catch { iconPath = null; }
            return (File.Exists(toolExe) ? toolExe : self, iconPath);
        }

        private static string Arg(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private static void Usage()
        {
            Log("Nucleus mod installer (Nuclear Option) — EARLY ALPHA");
            Log("  detect");
            Log("  install   [--game auto|<dir>] [--source <dir>] [--no-shortcut] [--dry-run]");
            Log("  launch    [--game auto|<dir>]");
            Log("  update    [--game auto|<dir>]");
            Log("  verify    [--game auto|<dir>] [--source <dir>]");
            Log("  uninstall [--game auto|<dir>] [--dry-run]");
            Log("Steam launch stays vanilla; the Nucleus desktop shortcut launches the mod.");
        }
    }
}
