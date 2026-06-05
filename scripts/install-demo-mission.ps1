<#
.SYNOPSIS
  Install the "Commander Debug" demo mission into the game's user-missions folder so it appears in the
  in-game mission browser. Writes only to the user profile (LocalLow), never the Steam install.
#>
[CmdletBinding()]
param([switch]$Force)
$ErrorActionPreference = 'Stop'

$repo = Split-Path $PSScriptRoot -Parent
$name = 'Commander Debug'
$src  = Join-Path $repo "missions\$name"
if (-not (Test-Path $src)) { throw "Demo mission template not found at $src" }

# UserMissionDirectory = <persistentDataPath>/Missions  (persistentDataPath = LocalLow\Shockfront\NuclearOption)
$missionsRoot = Join-Path $env:USERPROFILE 'AppData\LocalLow\Shockfront\NuclearOption\Missions'
$dest = Join-Path $missionsRoot $name

New-Item -ItemType Directory -Force $missionsRoot | Out-Null
if ((Test-Path $dest) -and -not $Force) {
    Write-Host "[mission] already installed at $dest (use -Force to overwrite)" -ForegroundColor Yellow
} else {
    if (Test-Path $dest) { Remove-Item -Recurse -Force $dest }
    Copy-Item -Recurse $src $dest
    Write-Host "[mission] installed '$name' -> $dest" -ForegroundColor Green
}
Write-Host "[mission] Launch the game; it appears in the mission browser as '$name'."
