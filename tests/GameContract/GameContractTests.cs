using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Xunit;

namespace CommanderLayer.GameContract.Tests
{
    /// <summary>
    /// Verifies the mod's assumptions about the REAL game assembly (lib/Assembly-CSharp.dll) using pure
    /// metadata inspection (Mono.Cecil — never executes Unity code). This is the automated proof that the
    /// members we call or reflect on actually exist with the expected types and accessibility, so a game
    /// update breaks CI here — not in the user's hands. Direct (typed) access is already guaranteed by the
    /// main project compiling against this same DLL; these tests additionally cover the string-based
    /// reflection targets and the public-state contract the runtime depends on.
    ///
    /// When lib/Assembly-CSharp.dll is absent (e.g. CI without the game), tests soft-skip (return).
    /// </summary>
    public class GameContractTests
    {
        // ---- VirtualMFD: the MFD bezel buttons we commandeer for "CMD" (private fields → reflection) ----
        [Fact]
        public void VirtualMFD_bezel_fields_exist_for_reflection()
        {
            if (!Game.Available) return;
            var t = Game.Type("VirtualMFD");

            AssertField(t, "leftButtons", isPublic: false, typeContains: "List`1", typeContains2: "UnityEngine.UI.Button");
            AssertField(t, "rightButtons", isPublic: false, typeContains: "List`1", typeContains2: "UnityEngine.UI.Button");
            AssertField(t, "leftScreens", isPublic: false, typeContains: "List`1", typeContains2: "MFDScreen");
            AssertField(t, "rightScreens", isPublic: false, typeContains: "List`1", typeContains2: "MFDScreen");

            var m = Method(t, "VirtualMFD_onMapMaximized");
            Assert.True(m.IsPublic, "VirtualMFD_onMapMaximized must be public (Harmony patch target).");
        }

        // ---- DynamicMap: tick driver + projection + open/close ----
        [Fact]
        public void DynamicMap_members_exist_and_are_accessible()
        {
            if (!Game.Available) return;
            var t = Game.Type("DynamicMap");

            AssertField(t, "mapDisplayFactor", isPublic: true, typeContains: "System.Single");
            AssertField(t, "iconLayer", isPublic: true, typeContains: "UnityEngine.GameObject");

            Assert.True(Method(t, "TryGetCursorCoordinates").IsPublic, "TryGetCursorCoordinates must be public.");
            Assert.True(Method(t, "Maximize").IsPublic, "Maximize must be public.");
            Assert.True(Method(t, "Minimize").IsPublic, "Minimize must be public.");
            Assert.NotNull(t.Methods.FirstOrDefault(x => x.Name == "Update")); // Harmony tick target (private ok)

            var prop = t.Properties.FirstOrDefault(p => p.Name == "mapMaximized");
            Assert.True(prop?.GetMethod != null && prop.GetMethod.IsPublic && prop.GetMethod.IsStatic,
                "mapMaximized must have a public static getter.");
        }

        // ---- Objective registration via the runner's PUBLIC state (no internal StartObjective) ----
        [Fact]
        public void MissionRunner_public_objective_state_exists()
        {
            if (!Game.Available) return;
            var runner = Game.Type("MissionRunner");
            AssertField(runner, "ActiveObjectives", isPublic: true, typeContains: "List`1");
            AssertField(runner, "activeByFaction", isPublic: true, typeContains: "Dictionary`2");
            Assert.True(Method(runner, "StopObjective").IsPublic, "StopObjective must be public.");

            var mm = Game.Type("MissionManager");
            var runnerProp = mm.Properties.FirstOrDefault(p => p.Name == "Runner");
            Assert.True(runnerProp?.GetMethod != null && runnerProp.GetMethod.IsPublic && runnerProp.GetMethod.IsStatic,
                "MissionManager.Runner must have a public static getter.");
        }

        // ---- CommanderObjective subclass safety: base is concrete, UpdateAndCheck is public-overridable ----
        [Fact]
        public void Objective_subclass_contract_is_loadable()
        {
            if (!Game.Available) return;

            var noObj = Game.Type("NuclearOption.SavedMission.ObjectiveV2.Objectives.NoObjective");
            Assert.False(noObj.IsAbstract, "NoObjective must be concrete (we subclass it).");
            var uac = Method(noObj, "UpdateAndCheck");
            Assert.True(uac.IsPublic && uac.IsVirtual, "NoObjective.UpdateAndCheck must be public+virtual to override.");

            var iface = Game.Type("NuclearOption.SavedMission.ObjectiveV2.IObjectiveWithPosition");
            Assert.True(iface.IsInterface && iface.Properties.Any(p => p.Name == "Positions"),
                "IObjectiveWithPosition must expose Positions.");

            var objPos = Game.Type("NuclearOption.SavedMission.ObjectiveV2.ObjectivePosition");
            Assert.Contains(objPos.Methods, m => m.IsConstructor && m.Parameters.Count == 2);

            var saved = Game.Type("NuclearOption.SavedMission.ObjectiveV2.SavedObjective");
            Assert.Contains(saved.Methods, m => m.IsConstructor && m.Parameters.Count == 2);

            var objType = Game.Type("NuclearOption.SavedMission.ObjectiveV2.ObjectiveType");
            Assert.Contains(objType.Fields, f => f.Name == "None");
        }

        // ---- Unit enumeration + local player/faction ----
        [Fact]
        public void Unit_and_player_members_exist()
        {
            if (!Game.Available) return;

            var reg = Game.Type("UnitRegistry");
            AssertField(reg, "allUnits", isPublic: true, typeContains: "List`1", typeContains2: "Unit");

            var unit = Game.Type("Unit");
            Assert.True(unit.Properties.Any(p => p.Name == "NetworkHQ"), "Unit.NetworkHQ property required.");
            AssertField(unit, "disabled", isPublic: true, typeContains: "System.Boolean");
            AssertField(unit, "unitName", isPublic: true, typeContains: "System.String");
            AssertField(unit, "definition", isPublic: true, typeContains: "UnitDefinition");

            var cmd = Game.Type("ICommandable");
            Assert.True(cmd.Properties.Any(p => p.Name == "UnitCommand"), "ICommandable.UnitCommand required.");

            var gm = Game.Type("GameManager");
            Assert.True(Method(gm, "GetLocalHQ").IsPublic && Method(gm, "GetLocalHQ").IsStatic, "GameManager.GetLocalHQ public static.");
            Assert.True(Method(gm, "GetLocalFaction").IsPublic && Method(gm, "GetLocalFaction").IsStatic, "GameManager.GetLocalFaction public static.");

            var faction = Game.Type("Faction");
            AssertField(faction, "factionName", isPublic: true, typeContains: "System.String");
            AssertField(faction, "color", isPublic: true, typeContains: "UnityEngine.Color");
        }

        // ---- GlobalPosition conversion ----
        [Fact]
        public void GlobalPosition_conversion_members_exist()
        {
            if (!Game.Available) return;
            var gp = Game.Type("GlobalPosition");
            Assert.Contains(gp.Methods, m => m.IsConstructor && m.Parameters.Count == 3
                && m.Parameters.All(p => p.ParameterType.FullName == "System.Single"));
            var asV = Method(gp, "AsVector3");
            Assert.Contains("Vector3", asV.ReturnType.FullName);
        }

        // ---- Every DIRECT reference from the built plugin into Assembly-CSharp must resolve ----
        [Fact]
        public void Plugin_game_references_all_resolve()
        {
            if (!Game.Available) return;
            string pluginPath = Game.FindPlugin();
            if (pluginPath == null) return; // plugin not built yet

            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(Game.LibDir);
            using var plugin = AssemblyDefinition.ReadAssembly(pluginPath, new ReaderParameters { AssemblyResolver = resolver });

            var unresolved = plugin.MainModule.GetMemberReferences()
                .Where(mr => mr.DeclaringType != null && ScopeName(mr.DeclaringType.Scope) == "Assembly-CSharp")
                .Where(mr => !TryResolve(mr))
                .Select(mr => mr.FullName)
                .ToList();

            Assert.True(unresolved.Count == 0,
                "Unresolved Assembly-CSharp references (game changed?):\n" + string.Join("\n", unresolved));
        }

        // ---------- helpers ----------
        private static bool TryResolve(MemberReference mr)
        {
            try
            {
                return mr switch
                {
                    MethodReference m => m.Resolve() != null,
                    FieldReference f => f.Resolve() != null,
                    _ => mr.DeclaringType.Resolve() != null
                };
            }
            catch { return false; }
        }

        private static string ScopeName(IMetadataScope scope) =>
            scope is AssemblyNameReference a ? a.Name : (scope?.Name?.Replace(".dll", "") ?? "");

        private static void AssertField(TypeDefinition t, string name, bool isPublic, string typeContains, string typeContains2 = null)
        {
            var f = t.Fields.FirstOrDefault(x => x.Name == name);
            Assert.True(f != null, $"{t.Name}.{name} field not found.");
            Assert.True(f.IsPublic == isPublic, $"{t.Name}.{name} expected IsPublic={isPublic} but was {f.IsPublic}.");
            Assert.Contains(typeContains, f.FieldType.FullName);
            if (typeContains2 != null) Assert.Contains(typeContains2, f.FieldType.FullName);
        }

        private static MethodDefinition Method(TypeDefinition t, string name)
        {
            var m = t.Methods.FirstOrDefault(x => x.Name == name);
            Assert.True(m != null, $"{t.Name}.{name} method not found.");
            return m;
        }
    }

    internal static class Game
    {
        public static readonly string LibDir = FindLib();
        public static bool Available => LibDir != null && File.Exists(Path.Combine(LibDir, "Assembly-CSharp.dll"));

        private static AssemblyDefinition _asm;

        public static ModuleDefinition Module
        {
            get
            {
                if (_asm == null)
                {
                    var resolver = new DefaultAssemblyResolver();
                    resolver.AddSearchDirectory(LibDir);
                    _asm = AssemblyDefinition.ReadAssembly(Path.Combine(LibDir, "Assembly-CSharp.dll"),
                        new ReaderParameters { AssemblyResolver = resolver });
                }
                return _asm.MainModule;
            }
        }

        public static TypeDefinition Type(string fullName)
        {
            var t = Module.GetType(fullName);
            Assert.True(t != null, $"Game type not found: {fullName}");
            return t;
        }

        private static string FindLib()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var lib = Path.Combine(dir.FullName, "lib");
                if (File.Exists(Path.Combine(lib, "Assembly-CSharp.dll"))) return lib;
                dir = dir.Parent;
            }
            return null;
        }

        public static string FindPlugin()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                foreach (var cfg in new[] { "Release", "Debug" })
                {
                    var p = Path.Combine(dir.FullName, "src", "bin", cfg, "netstandard2.1", "CommanderLayer.dll");
                    if (File.Exists(p)) return p;
                }
                dir = dir.Parent;
            }
            return null;
        }
    }
}
