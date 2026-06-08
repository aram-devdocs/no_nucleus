using System;
using System.IO;
using BepInEx.Logging;
using UnityEngine;

namespace Nucleus.Host
{
    /// <summary>
    /// Dev tool: dump the game's built-in mission TextAssets to disk so we can FORK one (e.g. Escalation) into a
    /// Nucleus mission. The built-in missions ship as Unity <c>Resources</c> TextAssets
    /// (<c>Resources.LoadAll&lt;TextAsset&gt;("Missions")</c> — see decompiled <c>MissionGroup.ResourceGroup</c>),
    /// so they have no loose file on disk; only code running INSIDE the game can read them. Gated on the
    /// <c>NUCLEUS_EXPORT_MISSIONS</c> environment variable so it never runs in normal play (and adds no overhead).
    /// Set the var to a target directory (or "1" to use the default next to the game) and launch once.
    /// </summary>
    internal static class MissionExporter
    {
        public static void MaybeExport(ManualLogSource log)
        {
            var flag = Environment.GetEnvironmentVariable("NUCLEUS_EXPORT_MISSIONS");
            if (string.IsNullOrEmpty(flag)) return;

            try
            {
                string outDir = flag == "1" || flag.Length < 2
                    ? Path.Combine(Application.dataPath, "..", "nucleus-missions-export")
                    : flag;
                Directory.CreateDirectory(outDir);

                var assets = Resources.LoadAll<TextAsset>("Missions");
                log.LogInfo($"[NUCLEUS:MISSIONEXPORT] found {assets?.Length ?? 0} built-in missions -> {outDir}");
                if (assets == null) return;

                foreach (var ta in assets)
                {
                    if (ta == null) continue;
                    var safe = string.Join("_", ta.name.Split(Path.GetInvalidFileNameChars()));
                    File.WriteAllText(Path.Combine(outDir, safe + ".json"), ta.text);
                    log.LogInfo($"[NUCLEUS:MISSIONEXPORT] wrote {ta.name} ({ta.text.Length} chars)");
                }
                log.LogInfo("[NUCLEUS:MISSIONEXPORT] done");
            }
            catch (Exception e)
            {
                log.LogError("[NUCLEUS:MISSIONEXPORT] failed: " + e);
            }
        }
    }
}
