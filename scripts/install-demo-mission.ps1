<#
.SYNOPSIS
  Install every bundled mission (each folder under missions/) into the game's user-missions folder so they
  appear in the in-game mission browser — including "Nucleus Dynamic Warfare" and "Commander Debug".
  Writes only to the user profile (LocalLow), never the Steam install.
#>
[CmdletBinding()]
param([switch]$Force)
$ErrorActionPreference = 'Stop'

$repo = Split-Path $PSScriptRoot -Parent
$missionsSrc = Join-Path $repo 'missions'
if (-not (Test-Path $missionsSrc)) { throw "Mission templates folder not found at $missionsSrc" }

# UserMissionDirectory = <persistentDataPath>/Missions  (persistentDataPath = LocalLow\Shockfront\NuclearOption)
$missionsRoot = Join-Path $env:USERPROFILE 'AppData\LocalLow\Shockfront\NuclearOption\Missions'
New-Item -ItemType Directory -Force $missionsRoot | Out-Null

foreach ($dir in Get-ChildItem -Directory $missionsSrc) {
    $name = $dir.Name
    $dest = Join-Path $missionsRoot $name
    if ((Test-Path $dest) -and -not $Force) {
        Write-Host "[mission] already installed: '$name' (use -Force to overwrite)" -ForegroundColor Yellow
        continue
    }
    if (Test-Path $dest) { Remove-Item -Recurse -Force $dest }
    Copy-Item -Recurse $dir.FullName $dest
    Write-Host "[mission] installed '$name' -> $dest" -ForegroundColor Green
}
Write-Host "[mission] Launch the game; the missions appear in the singleplayer mission browser."
