using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Xunit;

namespace Nucleus.Architecture.Tests
{
    /// <summary>
    /// Enforces the Nucleus dependency graph by reading the BUILT Nucleus.*.dll assemblies with Mono.Cecil
    /// (metadata only — never loads them). Keys on ASSEMBLY names (Nucleus.*). Until the libs exist the rule
    /// facts pass vacuously; the synthetic tests below prove the rules actually bite, so a vacuous pass can never
    /// be a false green. Run after building the solution (the audit script builds Nucleus.sln first).
    ///
    /// Rules: (1) pure libs reference no Unity/game/BepInEx; (2) no app references another app; (3) per-lib
    /// allowed Nucleus references encode the DAG + ownership; (4) the Nucleus.* reference graph is acyclic.
    /// </summary>
    public class ArchitectureTests
    {
        // ---- the rules run against the REAL built assemblies ----
        [Fact]
        public void Discovery_finds_repo_root() => Assert.NotNull(NucleusAssemblies.RepoRoot);

        [Fact]
        public void Pure_libs_are_unity_and_game_free()
            => Assert.Empty(Rules.PureLibsUnityFree(NucleusAssemblies.All));

        [Fact]
        public void No_app_references_another_app()
            => Assert.Empty(Rules.NoAppToApp(NucleusAssemblies.All));

        [Fact]
        public void Library_nucleus_references_match_the_allowed_dag()
            => Assert.Empty(Rules.LibRefsWithinAllowed(NucleusAssemblies.All));

        [Fact]
        public void Nucleus_reference_graph_is_acyclic()
            => Assert.Empty(Rules.Cycles(NucleusAssemblies.All));

        // ---- synthetic proofs that each rule catches a violation (guards against a false-green vacuous pass) ----
        [Fact]
        public void Rule_catches_pure_lib_touching_unity()
        {
            var bad = new[] { new AsmInfo("Nucleus.Domain", new[] { "UnityEngine.CoreModule" }) };
            Assert.NotEmpty(Rules.PureLibsUnityFree(bad));
        }

        [Fact]
        public void Rule_catches_app_referencing_app()
        {
            var bad = new[] { new AsmInfo("Nucleus.Commander", new[] { "Nucleus.Build", "Nucleus.Domain" }) };
            Assert.NotEmpty(Rules.NoAppToApp(bad));
        }

        [Fact]
        public void Rule_catches_disallowed_library_reference()
        {
            // Domain is the leaf — referencing Campaign is illegal (would invert the DAG).
            var bad = new[] { new AsmInfo("Nucleus.Domain", new[] { "Nucleus.Campaign" }) };
            Assert.NotEmpty(Rules.LibRefsWithinAllowed(bad));
        }

        [Fact]
        public void Rule_catches_a_cycle()
        {
            var bad = new[]
            {
                new AsmInfo("Nucleus.Squads", new[] { "Nucleus.Campaign" }),
                new AsmInfo("Nucleus.Campaign", new[] { "Nucleus.Squads" }),
            };
            Assert.NotEmpty(Rules.Cycles(bad));
        }
    }

    /// <summary>Pure, data-driven architecture rules (no I/O) so they're independently testable.</summary>
    internal static class Rules
    {
        public static readonly string[] PureLibs = { "Nucleus.Domain", "Nucleus.Squads", "Nucleus.Production", "Nucleus.Campaign", "Nucleus.Sim" };
        public static readonly string[] Apps = { "Nucleus.Platform", "Nucleus.Commander", "Nucleus.Build", "Nucleus.Squad", "Nucleus.Warfare" };

        public static readonly Dictionary<string, HashSet<string>> AllowedNucleusRefs = new()
        {
            ["Nucleus.Domain"] = new(),                                                          // leaf
            ["Nucleus.Squads"] = new() { "Nucleus.Domain" },
            ["Nucleus.Production"] = new() { "Nucleus.Domain" },
            ["Nucleus.Campaign"] = new() { "Nucleus.Domain", "Nucleus.Squads", "Nucleus.Production" },
            // The host contract exposes Theme (Ui) on IModUi and the shared ICampaign (Campaign) on IModContext,
            // so it references Domain + Ui + Campaign + Production (all pure — no Unity).
            ["Nucleus.Abstractions"] = new() { "Nucleus.Domain", "Nucleus.Ui", "Nucleus.Production", "Nucleus.Campaign" },
            // GameSdk is the engine-access integration layer: it converts game state into the domain types
            // and executes their outputs, so it may reference all four pure domain libs (but no app).
            ["Nucleus.GameSdk"] = new() { "Nucleus.Domain", "Nucleus.Squads", "Nucleus.Production", "Nucleus.Campaign" },
            // Ui hosts the shared campaign panel (CommanderPanel), so it reads the campaign read models.
            ["Nucleus.Ui"] = new() { "Nucleus.Domain", "Nucleus.Production", "Nucleus.Campaign" },
            // The headless sim/self-play lib runs the pure brain over a seeded battlefield — sits atop Campaign
            // (whose closure is Domain+Squads+Production), no Unity (consumed by tests + tools/Nucleus.Evolve).
            ["Nucleus.Sim"] = new() { "Nucleus.Domain", "Nucleus.Squads", "Nucleus.Production", "Nucleus.Campaign" },
        };

        public static bool IsGameOrUnity(string asmName) =>
            asmName.StartsWith("UnityEngine", StringComparison.Ordinal)
            || asmName is "Assembly-CSharp" or "Mirage" or "0Harmony" or "BepInEx" or "Unity.TextMeshPro";

        public static List<string> PureLibsUnityFree(IEnumerable<AsmInfo> asms)
        {
            var v = new List<string>();
            foreach (var a in asms.Where(a => PureLibs.Contains(a.Name)))
            {
                var bad = a.References.Where(IsGameOrUnity).ToList();
                if (bad.Count > 0) v.Add($"{a.Name} references Unity/game: {string.Join(", ", bad)}");
            }
            return v;
        }

        public static List<string> NoAppToApp(IEnumerable<AsmInfo> asms)
        {
            var v = new List<string>();
            foreach (var a in asms.Where(a => Apps.Contains(a.Name)))
            {
                var other = a.References.Where(r => Apps.Contains(r) && r != a.Name).ToList();
                if (other.Count > 0) v.Add($"{a.Name} references app(s): {string.Join(", ", other)}");
            }
            return v;
        }

        public static List<string> LibRefsWithinAllowed(IEnumerable<AsmInfo> asms)
        {
            var v = new List<string>();
            foreach (var a in asms)
            {
                if (!AllowedNucleusRefs.TryGetValue(a.Name, out var allowed)) continue; // apps unconstrained here
                var disallowed = a.References
                    .Where(r => r.StartsWith("Nucleus.", StringComparison.Ordinal) && !allowed.Contains(r)).ToList();
                if (disallowed.Count > 0)
                    v.Add($"{a.Name} references {string.Join(", ", disallowed)}; allowed [{string.Join(", ", allowed)}]");
            }
            return v;
        }

        public static List<string> Cycles(IEnumerable<AsmInfo> asms)
        {
            var list = asms.ToList();
            var nodes = list.Select(a => a.Name).ToHashSet();
            var graph = list.ToDictionary(
                a => a.Name,
                a => a.References.Where(r => r.StartsWith("Nucleus.", StringComparison.Ordinal) && r != a.Name && nodes.Contains(r)).ToHashSet());

            var indeg = graph.Keys.ToDictionary(k => k, _ => 0);
            foreach (var edges in graph.Values)
                foreach (var e in edges) indeg[e]++;
            var queue = new Queue<string>(indeg.Where(kv => kv.Value == 0).Select(kv => kv.Key));
            int visited = 0;
            while (queue.Count > 0)
            {
                var n = queue.Dequeue(); visited++;
                foreach (var e in graph[n]) if (--indeg[e] == 0) queue.Enqueue(e);
            }
            return visited == graph.Count
                ? new List<string>()
                : new List<string> { "cycle among: " + string.Join(", ", indeg.Where(kv => kv.Value > 0).Select(kv => kv.Key)) };
        }
    }

    /// <summary>An assembly's simple name + its referenced-assembly simple names.</summary>
    internal sealed record AsmInfo(string Name, IReadOnlyList<string> References);

    /// <summary>Discovers and Cecil-reads every built Nucleus.*.dll (the owning project's own output).</summary>
    internal static class NucleusAssemblies
    {
        public static readonly string RepoRoot = FindRepoRoot();

        private static List<AsmInfo> _all;
        public static IReadOnlyList<AsmInfo> All => _all ??= Discover();

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Nucleus.sln"))) return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }

        private static List<AsmInfo> Discover()
        {
            var result = new List<AsmInfo>();
            if (RepoRoot == null) return result;
            var seen = new HashSet<string>();
            var sep = Path.DirectorySeparatorChar;
            foreach (var dll in Directory.EnumerateFiles(RepoRoot, "Nucleus.*.dll", SearchOption.AllDirectories))
            {
                var name = Path.GetFileNameWithoutExtension(dll);
                if (name.EndsWith(".Tests")) continue;
                if (!dll.Contains($"{sep}{name}{sep}bin{sep}")) continue; // owning project's output only
                if (!seen.Add(name)) continue;
                try
                {
                    using var asm = AssemblyDefinition.ReadAssembly(dll);
                    result.Add(new AsmInfo(name, asm.MainModule.AssemblyReferences.Select(r => r.Name).Distinct().ToList()));
                }
                catch { /* unreadable/locked build artifact — skip */ }
            }
            return result;
        }
    }
}
