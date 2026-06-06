using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Nucleus.LogAudit
{
    public sealed class AuditCheck
    {
        public string Name { get; set; } = "";
        public bool Pass { get; set; }
        public string Detail { get; set; } = "";
    }

    public sealed class AuditReport
    {
        public bool Pass { get; set; }
        public List<AuditCheck> Checks { get; set; } = new();
        public List<string> Exceptions { get; set; } = new();
        public Dictionary<string, int> Metrics { get; set; } = new();

        public string ToJson() =>
            JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Turns a BepInEx/Player.log into a structured PASS/FAIL verdict for a playtest: did the plugin load, did
    /// the Harmony patches apply, did the host-driven tick reach the runtime, and were there any exceptions.
    /// Pure string analysis so it is fully unit-testable headlessly.
    /// </summary>
    public static class LogAuditor
    {
        public static AuditReport Analyze(IEnumerable<string> lines, int expectedPatches = 4)
        {
            var all = lines as IList<string> ?? lines.ToList();
            var r = new AuditReport();

            bool loaded = all.Any(l => l.Contains("Nucleus Platform loaded"));
            int patches = all.Count(l => l.Contains("Patched: "));
            bool firstTick = all.Any(l => l.Contains("first Tick"));

            var exceptions = all
                .Where(l => l.Contains("Exception") || l.Contains(" threw") || l.Contains("Unhandled"))
                .Distinct()
                .Take(25)
                .ToList();

            r.Metrics["patches"] = patches;
            r.Metrics["exceptions"] = exceptions.Count;
            r.Exceptions = exceptions;

            Add(r, "plugin-loaded", loaded, loaded ? "" : "'Nucleus Platform loaded.' not found");
            Add(r, "patches-applied", patches >= expectedPatches, $"{patches}/{expectedPatches}");
            Add(r, "runtime-tick", firstTick, firstTick ? "" : "'first Tick' not found (host tick may not reach the runtime)");
            Add(r, "no-exceptions", exceptions.Count == 0, exceptions.Count == 0 ? "" : $"{exceptions.Count} exception/error line(s)");

            // Structured self-test the host emits once the mission is live: each [NUCLEUS:SELFTEST] line becomes
            // a check; each [NUCLEUS:METRIC] k=v feeds the metrics. Optional (older logs lack them) — when
            // present they enrich and tighten the verdict.
            foreach (var l in all)
            {
                int si = l.IndexOf("[NUCLEUS:SELFTEST]", System.StringComparison.Ordinal);
                if (si >= 0)
                {
                    var parts = l.Substring(si + 18).Trim().Split(new[] { ' ' }, 2); // "PASS <name>"
                    if (parts.Length == 2)
                        Add(r, "selftest:" + parts[1].Trim(), parts[0] == "PASS", parts[0]);
                }
                int mi = l.IndexOf("[NUCLEUS:METRIC]", System.StringComparison.Ordinal);
                if (mi >= 0)
                {
                    var kv = l.Substring(mi + 16).Trim().Split('=');
                    if (kv.Length == 2 && int.TryParse(kv[1].Trim(), out var v))
                        r.Metrics[kv[0].Trim()] = v;
                }
            }

            r.Pass = r.Checks.All(c => c.Pass);
            return r;
        }

        private static void Add(AuditReport r, string name, bool pass, string detail) =>
            r.Checks.Add(new AuditCheck { Name = name, Pass = pass, Detail = detail });
    }
}
