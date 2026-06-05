<#
.SYNOPSIS
  Decompile the sandboxed Assembly-CSharp.dll into decompiled/ (gitignored) for the Phase 1 recon gate.
  Uses ilspycmd. Reads the game DLL only; writes only under the repo's decompiled/ folder.

.PARAMETER WholeProject
  Decompile the entire assembly to a project tree (slow). Default decompiles single-file output, which
  is easier to grep.
#>
[CmdletBinding()]
param([switch]$WholeProject)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$dll  = Join-Path $repo '.sandbox\game\NuclearOption_Data\Managed\Assembly-CSharp.dll'
$out  = Join-Path $repo 'decompiled'
if (-not (Test-Path $dll)) { throw "DLL not found at $dll. Run scripts/setup-sandbox.ps1 first." }
New-Item -ItemType Directory -Force $out | Out-Null

# Global dotnet tools may not be on PATH in a freshly-spawned shell; ensure they are.
$toolsDir = Join-Path $env:USERPROFILE '.dotnet\tools'
if (Test-Path $toolsDir) { $env:PATH = "$toolsDir;$env:PATH" }
if (-not (Get-Command 'ilspycmd' -ErrorAction SilentlyContinue)) {
    Write-Host "[decompile] installing ilspycmd (net8-compatible)" -ForegroundColor Cyan
    dotnet tool install -g ilspycmd --version 8.2.0.7535 | Out-Host
}

if ($WholeProject) {
    $proj = Join-Path $out 'Assembly-CSharp'
    New-Item -ItemType Directory -Force $proj | Out-Null
    Write-Host "[decompile] full project -> $proj" -ForegroundColor Cyan
    & ilspycmd $dll -p -o $proj | Out-Host
} else {
    $single = Join-Path $out 'Assembly-CSharp.decompiled.cs'
    Write-Host "[decompile] single file -> $single" -ForegroundColor Cyan
    & ilspycmd $dll -o $out
    # ilspycmd single-file mode writes <AssemblyName>.cs into -o dir; normalize the name.
    $produced = Join-Path $out 'Assembly-CSharp.cs'
    if (Test-Path $produced) { Move-Item $produced $single -Force }
}
Write-Host "[decompile] done. Grep decompiled/ for: ObjectiveData, AddNewObjective, UnitCommand, AIPilot, SetDestination" -ForegroundColor Green
