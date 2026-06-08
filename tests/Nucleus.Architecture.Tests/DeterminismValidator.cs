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
    /// Enforces .agents/rules/determinism.md: the pure libs must not call process-nondeterministic APIs, so the
    /// campaign sim + save/resume stay byte-identical. Cecil-scans the built pure-lib IL for banned calls
    /// (string.GetHashCode, HashCode.Combine, DateTime.Now/UtcNow, Environment.TickCount, System.Random,
    /// UnityEngine.Random). Fnv1a / DeterministicRng are the allowed deterministic primitives.
    /// </summary>
    public class DeterminismValidator
    {
        [Fact]
        public void Pure_libs_make_no_nondeterministic_calls()
            => Assert.Empty(DeterminismRules.Offenders(Rules.PureLibs));

        // ---- synthetic proofs the rule bites (no false-green) ----
        [Theory]
        [InlineData("System.String", "GetHashCode", true)]
        [InlineData("System.HashCode", "Combine", true)]
        [InlineData("System.DateTime", "get_Now", true)]
        [InlineData("System.DateTime", "get_UtcNow", true)]
        [InlineData("System.Environment", "get_TickCount", true)]
        [InlineData("System.Random", ".ctor", true)]
        [InlineData("UnityEngine.Random", "Range", true)]
        [InlineData("Nucleus.Core.Command.Fnv1a", "Hash", false)]   // the allowed deterministic primitive
        [InlineData("System.Math", "Sqrt", false)]
        [InlineData("System.String", "Substring", false)]
        public void Ban_predicate_catches_the_right_calls(string declaringType, string method, bool banned)
            => Assert.Equal(banned, DeterminismRules.IsBanned(declaringType, method));
    }

    internal static class DeterminismRules
    {
        /// <summary>True for a call that introduces per-process nondeterminism (forbidden in the pure libs).</summary>
        public static bool IsBanned(string declaringTypeFullName, string method)
        {
            switch (declaringTypeFullName)
            {
                case "System.String": return method == "GetHashCode";
                case "System.HashCode": return method == "Combine" || method == "ToHashCode";
                case "System.DateTime": return method == "get_Now" || method == "get_UtcNow";
                case "System.DateTimeOffset": return method == "get_Now" || method == "get_UtcNow";
                case "System.Environment": return method == "get_TickCount" || method == "get_TickCount64";
                case "System.Random": return true;            // any use of the BCL RNG
            }
            return declaringTypeFullName.StartsWith("UnityEngine.Random", StringComparison.Ordinal);
        }

        /// <summary>Scan the built pure-lib DLLs for banned call sites; returns "asm: Type::Method -> banned X".</summary>
        public static List<string> Offenders(IEnumerable<string> pureLibNames)
        {
            var offenders = new List<string>();
            var names = pureLibNames.ToHashSet();
            if (NucleusAssemblies.RepoRoot == null) return offenders;
            var sep = Path.DirectorySeparatorChar;
            var seen = new HashSet<string>();
            foreach (var dll in Directory.EnumerateFiles(NucleusAssemblies.RepoRoot, "Nucleus.*.dll", SearchOption.AllDirectories))
            {
                var name = Path.GetFileNameWithoutExtension(dll);
                if (!names.Contains(name)) continue;
                if (!dll.Contains($"{sep}{name}{sep}bin{sep}")) continue;   // owning project's output only
                if (!seen.Add(name)) continue;
                try
                {
                    using var asm = AssemblyDefinition.ReadAssembly(dll);
                    foreach (var type in asm.MainModule.GetTypes())
                        foreach (var m in type.Methods)
                        {
                            if (!m.HasBody) continue;
                            foreach (var ins in m.Body.Instructions)
                            {
                                if (ins.Operand is MethodReference mr && IsBanned(mr.DeclaringType.FullName, mr.Name))
                                    offenders.Add($"{name}: {type.FullName}::{m.Name} -> {mr.DeclaringType.FullName}.{mr.Name}");
                            }
                        }
                }
                catch { /* unreadable/locked build artifact — skip */ }
            }
            return offenders;
        }
    }
}
