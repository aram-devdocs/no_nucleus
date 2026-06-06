using System.IO;
using Nucleus.Installer;
using Xunit;

namespace Nucleus.Installer.Tests
{
    /// <summary>
    /// Headless tests for the mod installer: it copies only Nucleus.* plugin folders into the game's
    /// BepInEx/plugins, refuses when BepInEx is absent, honors dry-run, ignores foreign folders, and
    /// uninstalls cleanly — all in temp dirs, never touching a real game.
    /// </summary>
    public class InstallerTests
    {
        private static string NewDir()
        {
            var d = Path.Combine(Path.GetTempPath(), "nucleus-installer-tests", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(d);
            return d;
        }

        private static string MakeSource()
        {
            var src = NewDir();
            // Two Nucleus plugin folders (one carrying the shared libs) + a foreign folder to ignore.
            Directory.CreateDirectory(Path.Combine(src, "Nucleus.Platform"));
            File.WriteAllText(Path.Combine(src, "Nucleus.Platform", "Nucleus.Platform.dll"), "x");
            File.WriteAllText(Path.Combine(src, "Nucleus.Platform", "Nucleus.Domain.dll"), "x");
            Directory.CreateDirectory(Path.Combine(src, "Nucleus.Commander"));
            File.WriteAllText(Path.Combine(src, "Nucleus.Commander", "Nucleus.Commander.dll"), "x");
            Directory.CreateDirectory(Path.Combine(src, "SomeOtherMod"));
            File.WriteAllText(Path.Combine(src, "SomeOtherMod", "other.dll"), "x");
            return src;
        }

        private static string MakeGame(bool withBepInEx)
        {
            var game = NewDir();
            if (withBepInEx) Directory.CreateDirectory(Path.Combine(game, "BepInEx", "plugins"));
            return game;
        }

        [Fact]
        public void Install_copies_only_Nucleus_folders()
        {
            var src = MakeSource();
            var game = MakeGame(withBepInEx: true);

            var r = ModInstaller.Install(src, game, dryRun: false);

            Assert.True(r.Ok);
            Assert.Equal(2, r.Installed.Count);
            var plugins = Path.Combine(game, "BepInEx", "plugins");
            Assert.True(File.Exists(Path.Combine(plugins, "Nucleus.Platform", "Nucleus.Platform.dll")));
            Assert.True(File.Exists(Path.Combine(plugins, "Nucleus.Platform", "Nucleus.Domain.dll")));
            Assert.True(File.Exists(Path.Combine(plugins, "Nucleus.Commander", "Nucleus.Commander.dll")));
            Assert.False(Directory.Exists(Path.Combine(plugins, "SomeOtherMod"))); // foreign folder ignored
        }

        [Fact]
        public void Install_without_BepInEx_fails_and_writes_nothing()
        {
            var src = MakeSource();
            var game = MakeGame(withBepInEx: false);

            var r = ModInstaller.Install(src, game, dryRun: false);

            Assert.False(r.Ok);
            Assert.Empty(r.Installed);
            Assert.False(Directory.Exists(Path.Combine(game, "BepInEx", "plugins")));
        }

        [Fact]
        public void Dry_run_reports_but_writes_nothing()
        {
            var src = MakeSource();
            var game = MakeGame(withBepInEx: true);

            var r = ModInstaller.Install(src, game, dryRun: true);

            Assert.True(r.Ok);
            Assert.Equal(2, r.Installed.Count);
            Assert.False(Directory.Exists(Path.Combine(game, "BepInEx", "plugins", "Nucleus.Platform")));
        }

        [Fact]
        public void Install_overwrites_an_existing_install()
        {
            var src = MakeSource();
            var game = MakeGame(withBepInEx: true);
            ModInstaller.Install(src, game, dryRun: false);

            // Change a source file and reinstall — the target must reflect the new content.
            File.WriteAllText(Path.Combine(src, "Nucleus.Commander", "Nucleus.Commander.dll"), "v2");
            ModInstaller.Install(src, game, dryRun: false);

            var dll = Path.Combine(game, "BepInEx", "plugins", "Nucleus.Commander", "Nucleus.Commander.dll");
            Assert.Equal("v2", File.ReadAllText(dll));
        }

        [Fact]
        public void Uninstall_removes_only_Nucleus_folders()
        {
            var src = MakeSource();
            var game = MakeGame(withBepInEx: true);
            ModInstaller.Install(src, game, dryRun: false);
            // A foreign plugin the player installed separately must survive uninstall.
            var foreign = Path.Combine(game, "BepInEx", "plugins", "OtherPlugin");
            Directory.CreateDirectory(foreign);
            File.WriteAllText(Path.Combine(foreign, "x.dll"), "x");

            var r = ModInstaller.Uninstall(game, dryRun: false);

            Assert.True(r.Ok);
            Assert.Equal(2, r.Removed.Count);
            Assert.False(Directory.Exists(Path.Combine(game, "BepInEx", "plugins", "Nucleus.Platform")));
            Assert.True(Directory.Exists(foreign)); // foreign plugin untouched
        }
    }
}
