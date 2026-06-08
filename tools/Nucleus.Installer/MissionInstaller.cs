using System;
using System.IO;

namespace Nucleus.Installer
{
    /// <summary>Installs the bundled "Nucleus Dynamic Warfare" mission into the player's Nuclear Option Missions
    /// folder, so installer-only users (who didn't subscribe on Steam Workshop) still get the mission. Steam
    /// Workshop subscribers receive it through Steam; this is the GitHub-installer path.</summary>
    public static class MissionInstaller
    {
        /// <summary>%USERPROFILE%\AppData\LocalLow\Shockfront\NuclearOption\Missions (no SpecialFolder for LocalLow).</summary>
        public static string UserMissionsDir()
        {
            var profile = Environment.GetEnvironmentVariable("USERPROFILE");
            return string.IsNullOrEmpty(profile) ? null
                : Path.Combine(profile, "AppData", "LocalLow", "Shockfront", "NuclearOption", "Missions");
        }

        /// <summary>Copy each bundled mission folder (under &lt;sourceDir&gt;/missions) into the user Missions dir.
        /// Returns the count installed (0 when there are none to install).</summary>
        public static int Install(string sourceDir, Action<string> log)
        {
            var src = Path.Combine(sourceDir, "missions");
            if (!Directory.Exists(src)) return 0;
            var dest = UserMissionsDir();
            if (dest == null) { log("WARNING: could not resolve the user Missions folder; skipped the mission."); return 0; }

            Directory.CreateDirectory(dest);
            int n = 0;
            foreach (var m in Directory.GetDirectories(src))
            {
                CopyDir(m, Path.Combine(dest, Path.GetFileName(m)));
                log("installed mission: " + Path.GetFileName(m));
                n++;
            }
            return n;
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
