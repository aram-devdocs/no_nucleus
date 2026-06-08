using System.IO;
using Nucleus.Installer;
using Xunit;

namespace Nucleus.Installer.Tests
{
    /// <summary>Unit coverage for the installer's pure helpers: version comparison, plugin detection, the Doorstop
    /// toggle (vanilla-on-Steam default), and the version stamp. No game, no network — temp dirs only.</summary>
    public class InstallerCoreTests
    {
        [Theory]
        [InlineData("v0.2.0", "0.1.0", 1)]
        [InlineData("0.1.0", "0.1.0", 0)]
        [InlineData("v0.1.0", "0.2.0", -1)]
        [InlineData("v1.0.0", "0.9.9", 1)]
        public void Version_compare_orders_releases(string a, string b, int sign)
            => Assert.Equal(sign, System.Math.Sign(GitHubReleases.CompareVersions(a, b)));

        [Fact]
        public void HasPlugins_detects_a_nucleus_payload()
        {
            var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(Path.Combine(dir, "Nucleus.Platform"));
            Directory.CreateDirectory(Path.Combine(dir, "SomethingElse"));
            try
            {
                Assert.True(ModInstaller.HasPlugins(dir));
                Assert.False(ModInstaller.HasPlugins(Path.Combine(dir, "SomethingElse")));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void Doorstop_toggle_round_trips_and_leaves_other_keys_alone()
        {
            var game = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(game);
            var ini = Path.Combine(game, "doorstop_config.ini");
            File.WriteAllText(ini, "[General]\nenabled = true\ndebug_enabled = false\nredirect_output_log = false\n");
            try
            {
                DoorstopToggle.SetEnabled(game, false);
                Assert.True(DoorstopToggle.TryGet(game, out var off));
                Assert.False(off);
                DoorstopToggle.SetEnabled(game, true);
                Assert.True(DoorstopToggle.TryGet(game, out var on));
                Assert.True(on);
                // The 'enabled' toggle must not have clobbered debug_enabled.
                Assert.Contains("debug_enabled = false", File.ReadAllText(ini));
            }
            finally { Directory.Delete(game, true); }
        }

        [Fact]
        public void Version_stamp_writes_and_reads()
        {
            var game = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(game);
            try
            {
                Assert.Null(VersionStamp.ReadInstalled(game));
                VersionStamp.Write(game, "0.3.1");
                Assert.Equal("0.3.1", VersionStamp.ReadInstalled(game));
            }
            finally { Directory.Delete(game, true); }
        }

        [Fact]
        public void Mission_installer_copies_bundled_missions_to_the_user_folder()
        {
            var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var source = Path.Combine(root, "src");
            var profile = Path.Combine(root, "profile");
            var missionDir = Path.Combine(source, "missions", "Nucleus Dynamic Warfare");
            Directory.CreateDirectory(missionDir);
            File.WriteAllText(Path.Combine(missionDir, "Nucleus Dynamic Warfare.json"), "{}");
            var prev = System.Environment.GetEnvironmentVariable("USERPROFILE");
            try
            {
                System.Environment.SetEnvironmentVariable("USERPROFILE", profile);
                var n = MissionInstaller.Install(source, _ => { });
                Assert.Equal(1, n);
                Assert.True(File.Exists(Path.Combine(profile, "AppData", "LocalLow", "Shockfront",
                    "NuclearOption", "Missions", "Nucleus Dynamic Warfare", "Nucleus Dynamic Warfare.json")));
            }
            finally
            {
                System.Environment.SetEnvironmentVariable("USERPROFILE", prev);
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void Shipping_version_prefers_the_payload_stamp()
        {
            var src = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(src);
            File.WriteAllText(Path.Combine(src, "nucleus-version.txt"), "9.9.9\n");
            try { Assert.Equal("9.9.9", VersionStamp.Shipping(src)); }
            finally { Directory.Delete(src, true); }
        }
    }
}
