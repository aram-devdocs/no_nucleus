using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace Nucleus.Architecture.Tests
{
    /// <summary>Enforces the design-system SSOT: a raw <c>UnityEngine.Color</c> is constructed ONLY in the
    /// color-definition / procedural-sprite types (Theme, NativeColors, ObjectiveVisuals, UiFactory, NativeUi).
    /// Every other UI surface must pull its colors from a Theme/NativeColors/ObjectiveVisuals token. Cecil-scans
    /// each built Nucleus.* assembly's IL for `newobj UnityEngine.Color::.ctor` and fails on any other holder,
    /// so a panel can't quietly re-hardcode a color and drift from the palette.</summary>
    public class DesignSystemValidator
    {
        [Fact]
        public void Raw_colors_are_constructed_only_in_the_palette_definition_types()
            => Assert.Empty(DesignSystemRules.Offenders());

        [Theory]
        [InlineData("Nucleus.Ui.Theme", true)]
        [InlineData("Nucleus.Ui.NativeColors", true)]
        [InlineData("Nucleus.Ui.UiFactory", true)]
        [InlineData("Nucleus.Ui.Native.NativeUi", true)]
        [InlineData("Nucleus.Ui.CommanderPanel", false)]
        [InlineData("Nucleus.Composition.FlightHud", false)]
        public void Only_palette_types_may_define_raw_colors(string type, bool allowed)
            => Assert.Equal(allowed, DesignSystemRules.IsPaletteType(type));
    }

    internal static class DesignSystemRules
    {
        // The color SSOT: definitions (Theme/NativeColors/ObjectiveVisuals) + procedural sprite/border builders.
        private static readonly HashSet<string> Palette = new()
        {
            "Nucleus.Ui.Theme",
            "Nucleus.Ui.NativeColors",
            "Nucleus.Ui.ObjectiveVisuals",
            "Nucleus.Ui.UiFactory",
            "Nucleus.Ui.Native.NativeUi",
        };

        public static bool IsPaletteType(string fullName)
        {
            foreach (var p in Palette)
                if (fullName == p || fullName.StartsWith(p + "/")) return true;   // include nested closures
            return false;
        }

        /// <summary>"asm: Type constructs a raw UnityEngine.Color" for any non-palette holder.</summary>
        public static List<string> Offenders()
        {
            var offenders = new List<string>();
            if (NucleusAssemblies.RepoRoot == null) return offenders;
            var sep = Path.DirectorySeparatorChar;
            // Pick the NEWEST build per assembly (owning-project bin only), so a stale Debug copy from before a
            // refactor can't trip the gate while the current Release build is clean.
            var newest = new Dictionary<string, string>();
            foreach (var dll in Directory.EnumerateFiles(NucleusAssemblies.RepoRoot, "Nucleus.*.dll", SearchOption.AllDirectories))
            {
                var n = Path.GetFileNameWithoutExtension(dll);
                if (n.EndsWith(".Tests")) continue;
                if (!dll.Contains($"{sep}{n}{sep}bin{sep}")) continue;   // owning project's output only
                if (!newest.TryGetValue(n, out var prev) || File.GetLastWriteTimeUtc(dll) > File.GetLastWriteTimeUtc(prev))
                    newest[n] = dll;
            }
            foreach (var (name, dll) in newest)
            {
                try
                {
                    using var asm = AssemblyDefinition.ReadAssembly(dll);
                    foreach (var type in asm.MainModule.GetTypes())
                    {
                        if (IsPaletteType(type.FullName)) continue;
                        foreach (var m in type.Methods)
                        {
                            if (!m.HasBody) continue;
                            foreach (var ins in m.Body.Instructions)
                                if (ins.OpCode == OpCodes.Newobj && ins.Operand is MethodReference ctor
                                    && ctor.DeclaringType.FullName == "UnityEngine.Color")
                                    offenders.Add($"{name}: {type.FullName} constructs a raw UnityEngine.Color (use a Theme token)");
                        }
                    }
                }
                catch { /* unreadable/locked build artifact — skip */ }
            }
            return offenders.Distinct().ToList();
        }
    }
}
