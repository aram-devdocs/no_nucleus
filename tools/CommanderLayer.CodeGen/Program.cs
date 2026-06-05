using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Cecil;

// ============================================================================
// Commander typed-SDK generator.
//
// A SINGLE declarative manifest (below) of every game member the mod depends on
// is the source of truth. From it we emit, all verified against the REAL game
// assembly at generation time:
//
//   1. src/Core/Generated/GameEnums.generated.cs  — mirror enums PURE Core needs.
//   2. src/Core/Generated/GameRef.generated.cs     — verified member-name consts.
//   3. src/Game/Generated/GameSdk.generated.cs      — TYPED reflection accessors
//        for private members (types discovered from Cecil; no magic strings).
//   4. tests/GameContract/GameContract.Generated.cs — one contract test that
//        asserts EVERY manifest member still exists with the expected shape.
//
// Generation FAILS LOUDLY (listing every drifted member at once) if the game
// changed. So when the game updates: regenerate -> codegen and/or the compiler
// and/or CI point at exactly where the mod no longer fits. "Tight af."
//
// Discipline: list a member here only when the mod actually uses it.
// Run: dotnet run --project tools/CommanderLayer.CodeGen   (needs lib/Assembly-CSharp.dll)
// ============================================================================

// Enums PURE Core reasons about (the classifier). Mirrored so Core stays game-DLL-free.
string[] enumsToMirror = { "VehicleType", "ShipType", "BuildingType" };

// The dependency manifest. Kind ∈ {field, method, property, type}. Reflected=true emits a
// typed GameSdk accessor (private members reached without a compile-time reference).
var deps = new List<Dep>
{
    // ---- VirtualMFD: bezel buttons/screens we commandeer for the CMD button (reflected) ----
    new("VirtualMFD", "leftButtons",  "field", Reflected: true),
    new("VirtualMFD", "rightButtons", "field", Reflected: true),
    new("VirtualMFD", "leftScreens",  "field", Reflected: true),
    new("VirtualMFD", "rightScreens", "field", Reflected: true),

    // ---- DynamicMap: tick driver + projection + open/close ----
    new("DynamicMap", "mapDisplayFactor", "field", Public: true),
    new("DynamicMap", "iconLayer",        "field", Public: true),
    new("DynamicMap", "TryGetCursorCoordinates", "method", Public: true),
    new("DynamicMap", "Maximize", "method", Public: true),
    new("DynamicMap", "Minimize", "method", Public: true),
    new("DynamicMap", "Update",   "method"),                 // Harmony tick target (private ok)
    new("DynamicMap", "mapMaximized", "property", Public: true, Static: true),

    // ---- Unit enumeration + local player/faction ----
    new("UnitRegistry", "allUnits", "field", Public: true),
    new("Unit", "NetworkHQ",  "property"),
    new("Unit", "disabled",   "field", Public: true),
    new("Unit", "unitName",   "field", Public: true),
    new("Unit", "definition", "field", Public: true),
    new("Unit", "CaptureStrength", "property"),
    new("Unit", "persistentID", "field"),
    new("ICommandable", "UnitCommand", "property"),
    new("GameManager", "GetLocalHQ",      "method", Public: true, Static: true),
    new("GameManager", "GetLocalFaction", "method", Public: true, Static: true),
    new("GameManager", "GetLocalPlayer",  "method", Public: true, Static: true),
    new("Faction", "factionName", "field", Public: true),
    new("Faction", "color",       "field", Public: true),

    // ---- Unit tasking (movement / hold) ----
    new("UnitCommand", "SetDestination", "method", Public: true),
    new("Ship", "SetHoldPosition", "method", Public: true),
    new("GroundVehicle", "SetHoldPosition", "method", Public: true),

    // ---- GlobalPosition conversion ----
    new("GlobalPosition", "AsVector3", "method"),

    // ---- P2: production (commission) ----
    new("NuclearOption.Networking.Player", "CmdPurchaseConvoy", "method"),
    new("Faction", "GetConvoyGroups", "method"),
    new("Faction/ConvoyGroup", "GetCost", "method"),
    new("Faction/ConvoyGroup", "Name", "field"),
    new("FactionHQ", "factionFunds", "property", Public: true),

    // ---- P2: capture + logistics ----
    new("IRearmable", null, "type"),
    new("Airbase", "CurrentHQ", "property"),
    new("Airbase", "center",    "field"),

    // ---- P4: aircraft tasking (steer IDLE aircraft via the pilot's own no-target state — NOT a faction
    //         Objective, which the decompile shows also pulls idle ground/ships = the stampede). ----
    new("AIPilotCombatModes", "AssessHQTargets", "method"),
    new("AIPilotCombatModes", "NoTarget", "method"),         // postfix target (private)
    new("PilotBaseState", "aircraft",    "field", Reflected: true),  // protected Aircraft
    new("PilotBaseState", "destination", "field", Reflected: true),  // protected GlobalPosition (we override)
    new("CombatAI", "ChooseHQTarget", "method", Static: true),

    // ---- Reuse: native UI theme (GameAssets singleton — colors + font) ----
    new("GameAssets", "i", "property", Static: true),
    new("GameAssets", "HUDFriendly", "field", Public: true),
    new("GameAssets", "HUDHostile",  "field", Public: true),
    new("GameAssets", "HUDNeutral",  "field", Public: true),
    new("GameAssets", "playerNameFont", "field", Public: true),

    // ---- Reuse: native map tooltip range circles + lines (private — reflected) ----
    new("MapToolTip", "circle1", "field", Reflected: true),
    new("MapToolTip", "circle2", "field", Reflected: true),
    new("MapToolTip", "line1",   "field", Reflected: true),

    // ---- Reuse: game threat/intel queries (instead of re-deriving) ----
    new("FactionHQ", "trackingDatabase",   "field", Public: true),
    new("FactionHQ", "GetTargetsWithinRange", "method", Public: true),
    new("FactionHQ", "GetNearestGroundEnemy", "method", Public: true),
    new("FactionHQ", "GetAircraftThreat",     "method", Public: true),

    // ---- Reuse: game's own role/weapon suitability scoring ----
    new("RoleIdentity", "OpportunityAgainst", "method"),
    new("RoleIdentity", "antiSurface", "field", Public: true),
    new("RoleIdentity", "antiAir",     "field", Public: true),
};

string repoRoot = FindRepoRoot() ?? throw new Exception("Could not find repo root (lib/Assembly-CSharp.dll).");
string dllPath = Path.Combine(repoRoot, "lib", "Assembly-CSharp.dll");
string coreGenDir = Path.Combine(repoRoot, "src", "Core", "Generated");
string gameGenDir = Path.Combine(repoRoot, "src", "Game", "Generated");
string testGenDir = Path.Combine(repoRoot, "tests", "GameContract");
Directory.CreateDirectory(coreGenDir);
Directory.CreateDirectory(gameGenDir);

var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(Path.Combine(repoRoot, "lib"));
using var asm = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters { AssemblyResolver = resolver });
var module = asm.MainModule;

// Resolve types by SIMPLE name or full/nested name (Cecil uses '/' for nested).
TypeDefinition FindType(string name) =>
    module.GetType(name) ?? module.GetTypes().FirstOrDefault(t => t.Name == name);

// ---- verify EVERY manifest entry; collect ALL drift, fail once ----
var failures = new List<string>();
var resolved = new List<(Dep Dep, TypeDefinition Type, IMemberDefinition Member)>();
foreach (var d in deps)
{
    var t = FindType(d.Type);
    if (t == null) { failures.Add($"type '{d.Type}' not found (used for {d.Member ?? "<type>"})"); continue; }
    if (d.Kind == "type") { resolved.Add((d, t, null)); continue; }

    IMemberDefinition m = d.Kind switch
    {
        "field"    => t.Fields.FirstOrDefault(f => f.Name == d.Member),
        "method"   => t.Methods.FirstOrDefault(x => x.Name == d.Member),
        "property" => t.Properties.FirstOrDefault(p => p.Name == d.Member),
        _ => null
    };
    if (m == null) { failures.Add($"{d.Kind} '{d.Type}.{d.Member}' not found"); continue; }

    if (d.Public == true && !IsPublic(m)) failures.Add($"{d.Type}.{d.Member} expected public");
    if (d.Static && !IsStatic(m)) failures.Add($"{d.Type}.{d.Member} expected static");
    resolved.Add((d, t, m));
}
if (failures.Count > 0)
    throw new Exception("Manifest drift vs Assembly-CSharp (game changed?):\n  - " + string.Join("\n  - ", failures));

// ---- 1. enums ----
var e = Header("by tools/CommanderLayer.CodeGen from Assembly-CSharp.dll. DO NOT EDIT.",
               "Regenerate via scripts/generate-types.sh. Drift is asserted by a contract test.");
e.AppendLine("namespace CommanderLayer.Core.Generated");
e.AppendLine("{");
foreach (var name in enumsToMirror)
{
    var t = FindType(name);
    if (t == null || !t.IsEnum) throw new Exception($"Enum '{name}' not found in Assembly-CSharp.");
    e.AppendLine($"    /// <summary>Mirror of game enum <c>{name}</c>.</summary>");
    e.AppendLine($"    public enum {name}");
    e.AppendLine("    {");
    foreach (var f in t.Fields.Where(f => f.IsLiteral && f.Name != "value__"))
        e.AppendLine($"        {f.Name} = {Convert.ToInt64(f.Constant)},");
    e.AppendLine("    }");
    e.AppendLine();
}
e.AppendLine("}");
File.WriteAllText(Path.Combine(coreGenDir, "GameEnums.generated.cs"), e.ToString());

// ---- 2. reflected member-name constants (verified) — Core-visible, game-DLL-free ----
var reflected = resolved.Where(x => x.Dep.Reflected).ToList();
var r = Header("by tools/CommanderLayer.CodeGen from Assembly-CSharp.dll. DO NOT EDIT.",
               "Verified member names for reflection seams (no magic strings).");
r.AppendLine("namespace CommanderLayer.Core.Generated");
r.AppendLine("{");
r.AppendLine("    /// <summary>Verified names of private game members the mod reaches by reflection.</summary>");
r.AppendLine("    public static class GameRef");
r.AppendLine("    {");
foreach (var (d, t, _) in reflected)
    r.AppendLine($"        public const string {Ident(d.Type)}_{d.Member} = \"{d.Member}\";");
r.AppendLine("    }");
r.AppendLine("}");
File.WriteAllText(Path.Combine(coreGenDir, "GameRef.generated.cs"), r.ToString());

// ---- 3. typed reflection accessors (Game assembly — can name game types) ----
var s = Header("by tools/CommanderLayer.CodeGen from Assembly-CSharp.dll. DO NOT EDIT.",
               "Typed accessors for private game members. Types discovered from the real assembly.");
s.AppendLine("namespace CommanderLayer.Game.Generated");
s.AppendLine("{");
s.AppendLine("    /// <summary>Typed reflection seam into private game members (single source of truth).</summary>");
s.AppendLine("    public static class GameSdk");
s.AppendLine("    {");
foreach (var (d, t, m) in reflected)
{
    if (m is not FieldDefinition fd) throw new Exception($"Reflected member {d.Type}.{d.Member} must be a field for now.");
    string fieldType = CSharp(fd.FieldType);
    string ownerType = "global::" + t.FullName.Replace('/', '.');
    string acc = $"{Ident(d.Type)}_{d.Member}";
    s.AppendLine($"        private static readonly global::System.Reflection.FieldInfo _{acc} =");
    s.AppendLine($"            global::HarmonyLib.AccessTools.Field(typeof({ownerType}), \"{d.Member}\");");
    s.AppendLine($"        /// <summary>{d.Type}.{d.Member} (private {fd.FieldType.Name}) — get.</summary>");
    s.AppendLine($"        public static {fieldType} {acc}({ownerType} instance) => ({fieldType})_{acc}.GetValue(instance);");
    s.AppendLine($"        /// <summary>{d.Type}.{d.Member} — set.</summary>");
    s.AppendLine($"        public static void {acc}_Set({ownerType} instance, {fieldType} value) => _{acc}.SetValue(instance, value);");
    s.AppendLine();
}
s.AppendLine("    }");
s.AppendLine("}");
Directory.CreateDirectory(gameGenDir);
File.WriteAllText(Path.Combine(gameGenDir, "GameSdk.generated.cs"), s.ToString());

// ---- 4. generated contract test (Cecil-based; one test, lists all drift) ----
var c = Header("by tools/CommanderLayer.CodeGen. DO NOT EDIT.",
               "One test asserting every manifest member still exists with the expected shape.");
c.AppendLine("using System.Collections.Generic;");
c.AppendLine("using System.Linq;");
c.AppendLine("using Xunit;");
c.AppendLine();
c.AppendLine("namespace CommanderLayer.GameContract.Tests");
c.AppendLine("{");
c.AppendLine("    public class GeneratedManifestTests");
c.AppendLine("    {");
c.AppendLine("        [Fact]");
c.AppendLine("        public void Manifest_members_all_exist_with_expected_shape()");
c.AppendLine("        {");
c.AppendLine("            if (!Game.Available) return;");
c.AppendLine("            var fail = new List<string>();");
foreach (var (d, _, _) in resolved)
{
    string typeLit = Lit(d.Type);
    if (d.Kind == "type")
    {
        c.AppendLine($"            if (Game.Module.GetType({typeLit}) == null && Game.Module.GetTypes().All(t => t.Name != {typeLit})) fail.Add({Lit(d.Type + " (type)")});");
        continue;
    }
    string memLit = Lit(d.Member);
    string set = d.Kind switch { "field" => "Fields", "method" => "Methods", "property" => "Properties", _ => "Fields" };
    string pubChk = d.Public == true ? " && x.IsPublic" : "";
    string statChk = d.Static ? (d.Kind == "property" ? " && x.GetMethod != null && x.GetMethod.IsStatic" : " && x.IsStatic") : "";
    // properties have no IsPublic; gate public on the getter.
    if (d.Kind == "property" && d.Public == true) pubChk = " && x.GetMethod != null && x.GetMethod.IsPublic";
    c.AppendLine($"            if (!Game.Type({typeLit}).{set}.Any(x => x.Name == {memLit}{pubChk}{statChk})) fail.Add({Lit($"{d.Type}.{d.Member} ({d.Kind}{(d.Public == true ? ",public" : "")}{(d.Static ? ",static" : "")})")});");
}
c.AppendLine("            Assert.True(fail.Count == 0, \"Manifest drift vs Assembly-CSharp — run scripts/generate-types.sh:\\n  - \" + string.Join(\"\\n  - \", fail));");
c.AppendLine("        }");
c.AppendLine("    }");
c.AppendLine("}");
File.WriteAllText(Path.Combine(testGenDir, "GameContract.Generated.cs"), c.ToString());

Console.WriteLine($"Generated {enumsToMirror.Length} enum(s), {reflected.Count} typed accessor(s), {resolved.Count} contract assertion(s).");
return 0;

// ---------- helpers ----------
static StringBuilder Header(string what, string note)
{
    var b = new StringBuilder();
    b.AppendLine($"// <auto-generated> {what}");
    b.AppendLine($"//   {note}");
    b.AppendLine("#pragma warning disable CS1591");
    b.AppendLine();
    return b;
}

static bool IsPublic(IMemberDefinition m) => m switch
{
    FieldDefinition f => f.IsPublic,
    MethodDefinition me => me.IsPublic,
    PropertyDefinition p => p.GetMethod?.IsPublic == true,
    _ => false
};

static bool IsStatic(IMemberDefinition m) => m switch
{
    FieldDefinition f => f.IsStatic,
    MethodDefinition me => me.IsStatic,
    PropertyDefinition p => p.GetMethod?.IsStatic == true,
    _ => false
};

static string Ident(string typeName) => typeName.Split('.', '/').Last();
static string Lit(string s) => s == null ? "null" : "\"" + s + "\"";

// Render a Cecil TypeReference as a fully-qualified C# type string.
static string CSharp(TypeReference tr)
{
    if (tr is ArrayType at) return CSharp(at.ElementType) + "[]";
    if (tr is GenericInstanceType git)
    {
        string baseName = git.ElementType.FullName;
        int tick = baseName.IndexOf('`');
        if (tick >= 0) baseName = baseName.Substring(0, tick);
        string args = string.Join(", ", git.GenericArguments.Select(CSharp));
        return "global::" + baseName.Replace('/', '.') + "<" + args + ">";
    }
    return "global::" + tr.FullName.Replace('/', '.');
}

static string FindRepoRoot()
{
    foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        var dir = new DirectoryInfo(start);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "lib", "Assembly-CSharp.dll"))) return dir.FullName;
            dir = dir.Parent;
        }
    }
    return null;
}

record Dep(string Type, string Member, string Kind, bool? Public = null, bool Static = false, bool Reflected = false);
