<#
.SYNOPSIS
  Dump the game's built-in mission TextAssets (Escalation, etc.) to disk so they can be FORKED into Nucleus
  missions. The built-in missions ship as Unity Resources TextAssets (no loose file on disk), so only code
  running inside the game can read them - the platform mod's MissionExporter does the dump, gated on the
  NUCLEUS_EXPORT_MISSIONS environment variable. This script builds+deploys, boots the game with that var set,
  waits for the export to finish, kills the game, and copies the result into ./artifacts/missions-export.

  -NoBuild : skip the build/deploy step (use the already-deployed build).
#>
[CmdletBinding()]
param([switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$exe  = Join-Path $repo '.sandbox\game\NuclearOption.exe'
$dump = Join-Path $repo '.sandbox\game\nucleus-missions-export'
$dest = Join-Path $repo 'artifacts\missions-export'
if (-not (Test-Path $exe)) { throw "Sandbox exe not found at $exe (run scripts/setup-sandbox.ps1)." }

if (-not $NoBuild) {
    Write-Host "[export] build + deploy..." -ForegroundColor Cyan
    dotnet build (Join-Path $repo 'Nucleus.sln') -c Release -p:TreatWarningsAsErrors=true | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "build failed" }
}

$env:NUCLEUS_EXPORT_MISSIONS = '1'
Write-Host "[export] booting game to dump built-in missions..." -ForegroundColor Cyan
& (Join-Path $PSScriptRoot 'smoketest.ps1') -NoBuild -WaitMarker 'MISSIONEXPORT] done' -TimeoutSec 120 | Out-Null

if (-not (Test-Path $dump)) { throw "No export produced at $dump - did the platform mod load?" }
New-Item -ItemType Directory -Force $dest | Out-Null
Copy-Item (Join-Path $dump '*.json') $dest -Force
$n = (Get-ChildItem $dest -Filter *.json).Count
Write-Host "[export] $n missions -> $dest" -ForegroundColor Green
Write-Host "[export] fork one by copying it to 'missions/<Name>/<Name>.json' and editing the description." -ForegroundColor Yellow
