using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Nucleus.Installer
{
    /// <summary>
    /// CLI to install/uninstall the Nucleus mods into a Nuclear Option install. Ship it alongside the built
    /// plugin folders (Nucleus.Platform/, Nucleus.Commander/, …); by default it installs from its own folder.
    ///   Nucleus.Installer install   --game "C:\…\Nuclear Option" [--source &lt;dir&gt;] [--dry-run]
    ///   Nucleus.Installer uninstall --game "C:\…\Nuclear Option" [--dry-run]
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length == 0) { Usage(); return 2; }
            var cmd = args[0].ToLowerInvariant();
            string game = Arg(args, "--game");
            string source = Arg(args, "--source") ?? AppContext.BaseDirectory;
            bool dryRun = args.Any(a => a == "--dry-run");

            if (cmd != "install" && cmd != "uninstall") { Usage(); return 2; }
            if (string.IsNullOrEmpty(game)) { Console.Error.WriteLine("Missing --game <Nuclear Option folder>"); return 2; }

            var result = cmd == "install"
                ? ModInstaller.Install(source, game, dryRun)
                : ModInstaller.Uninstall(game, dryRun);

            var tag = dryRun ? " (dry-run)" : "";
            foreach (var n in result.Installed) Console.WriteLine($"  installed{tag}: {n}");
            foreach (var n in result.Removed) Console.WriteLine($"  removed{tag}: {n}");
            foreach (var e in result.Errors) Console.Error.WriteLine($"  ERROR: {e}");

            if (result.Ok)
                Console.WriteLine($"OK{tag} — plugins dir: {result.PluginsDir}");
            else
                Console.Error.WriteLine("FAILED — see errors above.");
            return result.Ok ? 0 : 1;
        }

        private static string Arg(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private static void Usage()
        {
            Console.WriteLine("Nucleus mod installer");
            Console.WriteLine("  install   --game <Nuclear Option folder> [--source <dir>] [--dry-run]");
            Console.WriteLine("  uninstall --game <Nuclear Option folder> [--dry-run]");
            Console.WriteLine("Requires BepInEx 5 (x64, Mono) already installed in the game folder.");
        }
    }
}
