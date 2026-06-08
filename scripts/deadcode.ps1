#requires -Version 5
<#
.SYNOPSIS
  Heuristic dead-code REPORT (not a gate — informational, always exits 0). Cecil-scans the built Nucleus.*
  assemblies for public/internal methods with no call-site anywhere in the set, to surface deletion
  candidates. The TreatWarningsAsErrors build already fails on unused PRIVATE members; this covers the
  public/internal gap that the compiler can't see across assemblies.

  REVIEW before deleting: interface implementations, virtual/override methods, reflection/serialization
  targets, and Unity/BepInEx lifecycle messages (Awake/Update/…) can show as "unused" yet are live. Build the
  solution first (the DLLs + Mono.Cecil must exist).
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$cecil = Get-ChildItem (Join-Path $root 'tests') -Recurse -Filter 'Mono.Cecil.dll' -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $cecil) { Write-Host '[deadcode] Mono.Cecil.dll not found — run a build first (dotnet build Nucleus.sln).'; exit 0 }
Add-Type -Path $cecil.FullName

# Owning-project output DLLs only (one per Nucleus.* assembly, excluding tests).
$byName = @{}
Get-ChildItem $root -Recurse -Filter 'Nucleus.*.dll' -ErrorAction SilentlyContinue | ForEach-Object {
    $n = $_.BaseName
    if ($n -notlike '*.Tests' -and $_.FullName -match "\\$([regex]::Escape($n))\\bin\\" -and -not $byName.ContainsKey($n)) {
        $byName[$n] = $_.FullName
    }
}
if ($byName.Count -eq 0) { Write-Host '[deadcode] no built Nucleus.*.dll found — build first.'; exit 0 }

$asms = foreach ($p in $byName.Values) { try { [Mono.Cecil.AssemblyDefinition]::ReadAssembly($p) } catch {} }

# Every method full-name that is CALLED somewhere in the set.
$called = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($a in $asms) {
    foreach ($t in $a.MainModule.GetTypes()) {
        foreach ($m in $t.Methods) {
            if (-not $m.HasBody) { continue }
            foreach ($i in $m.Body.Instructions) {
                if ($i.Operand -is [Mono.Cecil.MethodReference]) { [void]$called.Add($i.Operand.FullName) }
            }
        }
    }
}

$unityMsgs = 'Awake','Start','Update','LateUpdate','FixedUpdate','OnEnable','OnDisable','OnDestroy','OnGUI'
$candidates = New-Object System.Collections.Generic.List[string]
foreach ($a in $asms) {
    foreach ($t in $a.MainModule.GetTypes()) {
        $implementsInterface = $t.HasInterfaces
        foreach ($m in $t.Methods) {
            if (-not ($m.IsPublic -or $m.IsAssembly)) { continue }          # public/internal only
            if ($m.IsConstructor -or $m.IsGetter -or $m.IsSetter -or $m.IsAddOn -or $m.IsRemoveOn) { continue }
            if ($m.IsVirtual -or $m.IsAbstract -or $m.IsRuntimeSpecialName) { continue }  # polymorphic / special
            if ($implementsInterface) { continue }                          # interface impls are called via the iface
            if ($unityMsgs -contains $m.Name) { continue }                  # Unity/BepInEx lifecycle
            if ($m.Name -eq 'Main') { continue }                            # console entry point
            if ($t.Namespace -like '*.Generated') { continue }              # codegen output (contract-guarded)
            if (-not $called.Contains($m.FullName)) {
                $candidates.Add(("{0}: {1}.{2}" -f $a.Name, $t.FullName, $m.Name))
            }
        }
    }
}

Write-Host "[deadcode] scanned $($byName.Count) assemblies; $($candidates.Count) public/internal methods with no call-site (HEURISTIC, review before deleting):" -ForegroundColor Cyan
$candidates | Sort-Object | ForEach-Object { Write-Host "  $_" }
Write-Host '[deadcode] done (informational, exit 0).'
exit 0
