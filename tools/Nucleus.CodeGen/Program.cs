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
// Run: dotnet run --project tools/Nucleus.CodeGen   (needs lib/Assembly-CSharp.dll)
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

    // ---- MainMenu: clone a native menu button (missionsButton) to add a "NUCLEUS" entry natively ----
    new("MainMenu", "missionsButton",  "field", Reflected: true),
    new("MainMenu", "overlayMenuLayer", "field", Reflected: true),

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
    new("AIPilotCombatModes", "NoTarget", "method"),         // postfix target (private)
    new("PilotBaseState", "aircraft",    "field", Reflected: true),  // protected Aircraft
    new("PilotBaseState", "destination", "field", Reflected: true),  // protected GlobalPosition (we override)

    // ---- P6: native UI single-source-of-truth. GameAssets is the game's visual-resource singleton; every
    //         asset the mod uses is captured ONCE into a generated NativeAssets snapshot (Asset:true) so no
    //         color/font/icon value is ever hardcoded or duplicated in our UI. Cecil discovers each field's
    //         real type; the contract test guards them; a game update => regenerate => compiler points at
    //         exactly what drifted. ----
    new("GameAssets", "i", "property", Static: true),
    new("GameAssets", "playerNameFont", "field", Public: true, Asset: true), // native HUD font (used now)
    new("GameAssets", "HUDFriendly", "field", Public: true, Asset: true),    // friendly color (used now)
    new("GameAssets", "HUDHostile",  "field", Public: true, Asset: true),    // hostile color (used now)
    new("GameAssets", "HUDNeutral",  "field", Public: true, Asset: true),    // neutral color (P6.2 overlay)
    new("GameAssets", "airbaseSprite", "field", Public: true, Asset: true),  // map: airbase icon
    new("GameAssets", "targetUnitSprite", "field", Public: true, Asset: true), // map: enemy contact icon
    new("GameAssets", "targetUnitSpriteFriendly", "field", Public: true, Asset: true), // map: friendly icon
    new("GameAssets", "missileWarningSprite", "field", Public: true, Asset: true),     // threat: missile warning
    new("GameAssets", "warheadSprite", "field", Public: true, Asset: true),  // threat/strike icon

    // ---- P6: native UI components the mod clones/configures so its UI IS the game's UI. BetterBorder is
    //         already used LIVE (frames our panel) — guarding it here means a game rename fails the contract
    //         instead of silently breaking the panel. The toggles + group are the P6.2 harvest targets
    //         (clone a live instance, rebind onValueChanged); their public surface is asserted to exist. ----
    new("NuclearOption.UI.BetterBorder", "BorderThickness", "property", Public: true),
    new("NuclearOption.UI.BetterBorder", "FillColor",       "property", Public: true),
    new("NuclearOption.UI.BetterBorder", "color",           "property", Public: true),
    new("NuclearOption.UI.BaseToggle", "isOn",                 "property", Public: true),
    new("NuclearOption.UI.BaseToggle", "onValueChanged",       "field",    Public: true),
    new("NuclearOption.UI.BaseToggle", "SetIsOnWithoutNotify", "method",   Public: true),
    new("NuclearOption.UI.BoxToggle",    null, "type"),
    new("NuclearOption.UI.SliderToggle", null, "type"),
    new("NuclearOption.UI.BetterToggleGroup", "SetIndex", "method", Public: true),
    new("NuclearOption.UI.BetterToggleGroup", "GetIndex", "method", Public: true),
    new("NuclearOption.UI.BetterToggleGroup", "SetFlags", "method", Public: true),
    new("NuclearOption.UI.BetterToggleGroup", "GetFlags", "method", Public: true),

    // ---- Reuse: game role data the classifier reads (fog-of-war intel uses trackingDatabase) ----
    new("FactionHQ", "trackingDatabase", "field", Public: true),
    new("RoleIdentity", "antiSurface", "field", Public: true),
    new("RoleIdentity", "antiAir",     "field", Public: true),
};

string repoRoot = FindRepoRoot() ?? throw new Exception("Could not find repo root (lib/Assembly-CSharp.dll).");
string dllPath = Path.Combine(repoRoot, "lib", "Assembly-CSharp.dll");
// Core mirrors (GameEnums/GameRef, namespace Nucleus.Core.Generated) live in the extracted pure
// leaf lib Nucleus.Domain. gameGenDir (GameSdk/NativeAssets) relocates to its lib in Phase 2.
string coreGenDir = Path.Combine(repoRoot, "libs", "Nucleus.Domain", "Generated");
string gameGenDir = Path.Combine(repoRoot, "libs", "Nucleus.GameSdk", "Generated");
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
var e = Header("by tools/Nucleus.CodeGen from Assembly-CSharp.dll. DO NOT EDIT.",
               "Regenerate via scripts/generate-types.sh. Drift is asserted by a contract test.");
e.AppendLine("namespace Nucleus.Core.Generated");
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
var r = Header("by tools/Nucleus.CodeGen from Assembly-CSharp.dll. DO NOT EDIT.",
               "Verified member names for reflection seams (no magic strings).");
r.AppendLine("namespace Nucleus.Core.Generated");
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
var s = Header("by tools/Nucleus.CodeGen from Assembly-CSharp.dll. DO NOT EDIT.",
               "Typed accessors for private game members. Types discovered from the real assembly.");
s.AppendLine("namespace Nucleus.Game.Generated");
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

// ---- 3b. NativeAssets snapshot (P6): typed one-shot capture of GameAssets visual resources, so the UI
//          reads native font/colors/icons from a SINGLE source instead of hardcoding/duplicating them. ----
var assetDeps = resolved.Where(x => x.Dep.Asset).ToList();
foreach (var (d, _, m) in assetDeps)
    if (m is not FieldDefinition) throw new Exception($"Asset member {d.Type}.{d.Member} must be a public field.");
var na = Header("by tools/Nucleus.CodeGen from Assembly-CSharp.dll. DO NOT EDIT.",
                "Typed snapshot of GameAssets visual resources — the single source of truth for native UI.");
na.AppendLine("namespace Nucleus.Game.Generated");
na.AppendLine("{");
na.AppendLine("    /// <summary>One-shot typed snapshot of the game's <c>GameAssets</c> visual resources (font,");
na.AppendLine("    /// HUD colors, map/threat icons). Captured once from <c>GameAssets.i</c>; the UI reads native");
na.AppendLine("    /// values from here so nothing is hardcoded or duplicated. Regenerated from the manifest —");
na.AppendLine("    /// any drift fails the GameContract test.</summary>");
na.AppendLine("    public sealed class NativeAssets");
na.AppendLine("    {");
foreach (var (d, _, m) in assetDeps)
{
    var fd = (FieldDefinition)m;
    na.AppendLine($"        /// <summary>GameAssets.{d.Member} ({fd.FieldType.Name}).</summary>");
    na.AppendLine($"        public readonly {CSharp(fd.FieldType)} {d.Member};");
}
na.AppendLine();
na.AppendLine("        private NativeAssets(global::GameAssets a)");
na.AppendLine("        {");
foreach (var (d, _, _) in assetDeps)
    na.AppendLine($"            {d.Member} = a.{d.Member};");
na.AppendLine("        }");
na.AppendLine();
na.AppendLine("        /// <summary>Capture the live snapshot, or <c>null</c> if GameAssets isn't loaded yet.</summary>");
na.AppendLine("        public static NativeAssets Capture()");
na.AppendLine("        {");
na.AppendLine("            var a = global::GameAssets.i;");
na.AppendLine("            return a == null ? null : new NativeAssets(a);");
na.AppendLine("        }");
na.AppendLine("    }");
na.AppendLine("}");
File.WriteAllText(Path.Combine(gameGenDir, "NativeAssets.generated.cs"), na.ToString());

// ---- 4. generated contract test (Cecil-based; one test, lists all drift) ----
var c = Header("by tools/Nucleus.CodeGen. DO NOT EDIT.",
               "One test asserting every manifest member still exists with the expected shape.");
c.AppendLine("using System.Collections.Generic;");
c.AppendLine("using System.Linq;");
c.AppendLine("using Xunit;");
c.AppendLine();
c.AppendLine("namespace Nucleus.GameContract.Tests");
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

Console.WriteLine($"Generated {enumsToMirror.Length} enum(s), {reflected.Count} typed accessor(s), {assetDeps.Count} native asset(s), {resolved.Count} contract assertion(s).");
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

record Dep(string Type, string Member, string Kind, bool? Public = null, bool Static = false, bool Reflected = false, bool Asset = false);
