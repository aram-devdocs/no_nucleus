using System.Collections.Generic;
using System.Linq;
using Nucleus.LogAudit;
using Xunit;

namespace Nucleus.LogAudit.Tests
{
    public class LogAuditTests
    {
        private static List<string> HealthyLog() => new()
        {
            "[Info   :   BepInEx] Loading [Commander Layer 0.1.0]",
            "[Info   :Commander Layer] Nucleus Platform loaded.",
            "[Info   :Commander Layer] Patched: MainMenuBadgePatch",
            "[Info   :Commander Layer] Patched: DynamicMapUpdateTickPatch",
            "[Info   :Commander Layer] Patched: VirtualMFDPatch",
            "[Info   :Commander Layer] Patched: AircraftTaskingPatch",
            "[Info   :Commander Layer] CommanderRuntime first Tick — driver alive.",
            "[Info   :Commander Layer] Commander panel built.",
        };

        [Fact]
        public void Healthy_log_passes()
        {
            var r = LogAuditor.Analyze(HealthyLog());
            Assert.True(r.Pass);
            Assert.Equal(4, r.Metrics["patches"]);
            Assert.Equal(0, r.Metrics["exceptions"]);
        }

        [Fact]
        public void Exception_line_fails_the_audit()
        {
            var log = HealthyLog();
            log.Add("[Error  :Commander Layer] Update tick threw: System.NullReferenceException: ...");
            var r = LogAuditor.Analyze(log);
            Assert.False(r.Pass);
            Assert.Contains(r.Checks, c => c.Name == "no-exceptions" && !c.Pass);
            Assert.Equal(1, r.Metrics["exceptions"]);
        }

        [Fact]
        public void Missing_patches_fails_the_audit()
        {
            var log = HealthyLog().Where(l => !l.Contains("VirtualMFDPatch") && !l.Contains("AircraftTaskingPatch")).ToList();
            var r = LogAuditor.Analyze(log);
            Assert.False(r.Pass);
            Assert.Contains(r.Checks, c => c.Name == "patches-applied" && !c.Pass);
        }

        [Fact]
        public void Missing_first_tick_fails_runtime_check()
        {
            var log = HealthyLog().Where(l => !l.Contains("first Tick")).ToList();
            var r = LogAuditor.Analyze(log);
            Assert.Contains(r.Checks, c => c.Name == "runtime-tick" && !c.Pass);
        }

        [Fact]
        public void Structured_selftest_lines_become_checks_and_metrics()
        {
            var log = HealthyLog();
            log.Add("[Info   :Commander Layer] [NUCLEUS:METRIC] mods=1");
            log.Add("[Info   :Commander Layer] [NUCLEUS:METRIC] roster=12");
            log.Add("[Info   :Commander Layer] [NUCLEUS:SELFTEST] PASS host-tick-alive");
            log.Add("[Info   :Commander Layer] [NUCLEUS:SELFTEST] PASS mods-registered");
            var r = LogAuditor.Analyze(log);
            Assert.True(r.Pass);
            Assert.Equal(12, r.Metrics["roster"]);
            Assert.Equal(1, r.Metrics["mods"]);
            Assert.Contains(r.Checks, c => c.Name == "selftest:host-tick-alive" && c.Pass);
        }

        [Fact]
        public void A_failed_selftest_fails_the_audit()
        {
            var log = HealthyLog();
            log.Add("[Info   :Commander Layer] [NUCLEUS:SELFTEST] FAIL mods-registered");
            var r = LogAuditor.Analyze(log);
            Assert.False(r.Pass);
            Assert.Contains(r.Checks, c => c.Name == "selftest:mods-registered" && !c.Pass);
        }

        [Fact]
        public void Report_serializes_to_json()
        {
            var json = LogAuditor.Analyze(HealthyLog()).ToJson();
            Assert.Contains("\"Pass\": true", json);
            Assert.Contains("plugin-loaded", json);
        }
    }
}
