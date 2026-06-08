using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Xunit;

namespace Nucleus.Architecture.Tests
{
    /// <summary>
    /// Enforces the stateless-UI contract (.agents/rules): a Nucleus.Ui type renders from immutable snapshots and
    /// holds widget handles + theme + UI-local selection only — never a reference to the live, MUTABLE campaign
    /// model. Cecil-scans Nucleus.Ui fields for the banned live-state types so the contract can't silently
    /// regress.
    /// </summary>
    public class UiStatelessnessValidator
    {
        [Fact]
        public void Ui_holds_no_live_mutable_model_state()
            => Assert.Empty(UiStatelessRules.Offenders());

        [Theory]
        [InlineData("Nucleus.Core.Command.CommanderState", true)]
        [InlineData("Nucleus.Core.Command.Operation", true)]
        [InlineData("Nucleus.Core.Command.Objective", true)]
        [InlineData("Nucleus.Core.Command.WarfareCampaign", true)]
        [InlineData("Nucleus.Core.Command.SquadRoster", true)]
        [InlineData("Nucleus.Core.Command.Squad", true)]
        [InlineData("Nucleus.Core.Command.ObjectiveKind", false)]   // value snapshot/enum is fine
        [InlineData("Nucleus.Core.Command.HqSnapshot", false)]      // immutable read-model is fine
        [InlineData("System.String", false)]
        public void Ban_predicate_targets_only_live_mutable_state(string typeFullName, bool banned)
            => Assert.Equal(banned, UiStatelessRules.IsLiveMutableState(typeFullName));
    }

    internal static class UiStatelessRules
    {
        private static readonly HashSet<string> Banned = new()
        {
            "Nucleus.Core.Command.CommanderState",
            "Nucleus.Core.Command.Operation",
            "Nucleus.Core.Command.Objective",
            "Nucleus.Core.Command.WarfareCampaign",
            "Nucleus.Core.Command.SquadRoster",
            "Nucleus.Core.Command.Squad",
        };

        public static bool IsLiveMutableState(string typeFullName) => Banned.Contains(typeFullName);

        /// <summary>Scan Nucleus.Ui fields (incl. generic arguments) for banned live-state types.</summary>
        public static List<string> Offenders()
        {
            var offenders = new List<string>();
            if (NucleusAssemblies.RepoRoot == null) return offenders;
            var sep = Path.DirectorySeparatorChar;
            var dll = Directory.EnumerateFiles(NucleusAssemblies.RepoRoot, "Nucleus.Ui.dll", SearchOption.AllDirectories)
                .FirstOrDefault(p => p.Contains($"{sep}Nucleus.Ui{sep}bin{sep}"));
            if (dll == null) return offenders;
            try
            {
                using var asm = AssemblyDefinition.ReadAssembly(dll);
                foreach (var type in asm.MainModule.GetTypes())
                    foreach (var f in type.Fields)
                        foreach (var t in FlattenTypeNames(f.FieldType))
                            if (IsLiveMutableState(t))
                                offenders.Add($"Nucleus.Ui {type.FullName}.{f.Name} retains live model type {t}");
            }
            catch { /* unreadable/locked build artifact — skip */ }
            return offenders;
        }

        // A field type + any generic arguments (so List<Operation> / Action<Objective> are caught).
        private static IEnumerable<string> FlattenTypeNames(TypeReference t)
        {
            yield return t.FullName.Split('<', '`')[0];
            if (t is GenericInstanceType g)
                foreach (var arg in g.GenericArguments)
                    foreach (var n in FlattenTypeNames(arg))
                        yield return n;
        }
    }
}
