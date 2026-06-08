using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Xunit;

namespace Nucleus.Architecture.Tests
{
    /// <summary>
    /// Enforces "apps are thin" (.agents/rules): an app plugin composes libs and wires them to the game — it must
    /// not host pure decision logic. Conservatively: each app assembly references at least one Nucleus lib and
    /// declares no type whose name ends in a decision-logic suffix (Brain/Planner/Roster/Genome/Doctrine), which
    /// belong in the pure libs.
    /// </summary>
    public class AppThinnessValidator
    {
        [Fact]
        public void Apps_reference_a_lib_and_host_no_decision_logic_types()
            => Assert.Empty(AppThinnessRules.Offenders(NucleusAssemblies.All));

        [Theory]
        [InlineData("CommanderBrain", true)]
        [InlineData("OrderPlanner", true)]
        [InlineData("SquadRoster", true)]
        [InlineData("CommanderGenome", true)]
        [InlineData("CommanderRuntime", false)]   // composition/wiring is fine in an app
        [InlineData("WarfareMod", false)]
        public void Decision_logic_suffix_predicate(string typeName, bool isDecisionLogic)
            => Assert.Equal(isDecisionLogic, AppThinnessRules.IsDecisionLogicName(typeName));

        // synthetic proof the assembly rule bites
        [Fact]
        public void Rule_catches_an_app_with_no_lib_reference()
            => Assert.NotEmpty(AppThinnessRules.AssemblyOffenders(new[] { new AsmInfo("Nucleus.Commander", new[] { "UnityEngine" }) }));
    }

    internal static class AppThinnessRules
    {
        private static readonly string[] DecisionSuffixes = { "Brain", "Planner", "Roster", "Genome", "Doctrine" };

        public static bool IsDecisionLogicName(string typeName) =>
            DecisionSuffixes.Any(s => typeName.EndsWith(s, StringComparison.Ordinal));

        /// <summary>App assemblies that reference no Nucleus lib (apps must build ON the libs).</summary>
        public static List<string> AssemblyOffenders(IEnumerable<AsmInfo> asms)
        {
            var v = new List<string>();
            foreach (var a in asms.Where(a => Rules.Apps.Contains(a.Name)))
            {
                bool refsLib = a.References.Any(r => r.StartsWith("Nucleus.", StringComparison.Ordinal) && !Rules.Apps.Contains(r));
                if (!refsLib) v.Add($"{a.Name} references no Nucleus lib (an app must compose the libs)");
            }
            return v;
        }

        /// <summary>App offenders: missing lib reference, or a decision-logic-named type declared in the app.</summary>
        public static List<string> Offenders(IEnumerable<AsmInfo> asms)
        {
            var v = AssemblyOffenders(asms);
            if (NucleusAssemblies.RepoRoot == null) return v;
            var sep = Path.DirectorySeparatorChar;
            var seen = new HashSet<string>();
            foreach (var dll in Directory.EnumerateFiles(NucleusAssemblies.RepoRoot, "Nucleus.*.dll", SearchOption.AllDirectories))
            {
                var name = Path.GetFileNameWithoutExtension(dll);
                if (!Rules.Apps.Contains(name)) continue;
                if (!dll.Contains($"{sep}{name}{sep}bin{sep}")) continue;
                if (!seen.Add(name)) continue;
                try
                {
                    using var asm = AssemblyDefinition.ReadAssembly(dll);
                    foreach (var type in asm.MainModule.GetTypes())
                        if (!type.Name.Contains('<') && IsDecisionLogicName(type.Name))
                            v.Add($"{name}: app declares decision-logic type {type.Name} (belongs in a lib)");
                }
                catch { /* unreadable/locked build artifact — skip */ }
            }
            return v;
        }
    }
}
