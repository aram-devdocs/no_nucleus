using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace Nucleus.Architecture.Tests
{
    /// <summary>
    /// Enforces the wording SSOT: the human ObjectiveKind names ("Capture point", "Destroy target", …) are owned
    /// by Nucleus.Core.Command.ObjectiveText and nowhere else (ObjectiveVisuals delegates to it). Cecil-scans
    /// every built Nucleus.* assembly's string constants for those literals and fails if any type other than
    /// ObjectiveText emits one — so a future surface can't re-hardcode (and drift) the labels.
    /// </summary>
    public class SsotValidator
    {
        [Fact]
        public void Objective_wording_literals_live_only_in_ObjectiveText()
            => Assert.Empty(SsotRules.OffendingTypes());

        [Theory]
        [InlineData("Capture point", true)]
        [InlineData("Destroy target", true)]
        [InlineData("Control airspace", true)]
        [InlineData("Holding", false)]          // a phase label, not an objective name
        [InlineData("anything else", false)]
        public void Wording_set_is_the_objective_names(string s, bool isWording)
            => Assert.Equal(isWording, SsotRules.IsObjectiveWording(s));
    }

    internal static class SsotRules
    {
        private const string Owner = "Nucleus.Core.Command.ObjectiveText";

        private static readonly HashSet<string> Wording = new()
        {
            "Capture point", "Destroy target", "Defend area", "Control airspace",
            "Suppress air defense", "Naval strike",
        };

        public static bool IsObjectiveWording(string s) => Wording.Contains(s);

        /// <summary>"asm: Type emits objective-wording literal 'X'" for any non-ObjectiveText holder.</summary>
        public static List<string> OffendingTypes()
        {
            var offenders = new List<string>();
            if (NucleusAssemblies.RepoRoot == null) return offenders;
            var sep = Path.DirectorySeparatorChar;
            var seen = new HashSet<string>();
            foreach (var dll in Directory.EnumerateFiles(NucleusAssemblies.RepoRoot, "Nucleus.*.dll", SearchOption.AllDirectories))
            {
                var name = Path.GetFileNameWithoutExtension(dll);
                if (name.EndsWith(".Tests")) continue;
                if (!dll.Contains($"{sep}{name}{sep}bin{sep}")) continue;   // owning project's output only
                if (!seen.Add(name)) continue;
                try
                {
                    using var asm = AssemblyDefinition.ReadAssembly(dll);
                    foreach (var type in asm.MainModule.GetTypes())
                    {
                        if (type.FullName == Owner) continue;
                        foreach (var m in type.Methods)
                        {
                            if (!m.HasBody) continue;
                            foreach (var ins in m.Body.Instructions)
                                if (ins.OpCode == OpCodes.Ldstr && ins.Operand is string s && IsObjectiveWording(s))
                                    offenders.Add($"{name}: {type.FullName} emits objective-wording literal '{s}'");
                        }
                    }
                }
                catch { /* unreadable/locked build artifact — skip */ }
            }
            return offenders.Distinct().ToList();
        }
    }
}
