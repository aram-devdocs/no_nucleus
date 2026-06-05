<#
.SYNOPSIS
  Launch the sandboxed modded Nuclear Option. Steam must be running. Never touches the Steam install.
#>
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$exe  = Join-Path $repo '.sandbox\game\NuclearOption.exe'
if (-not (Test-Path $exe)) { throw "Sandbox exe not found at $exe. Run scripts/setup-sandbox.ps1 first." }
Write-Host "[run] launching $exe" -ForegroundColor Cyan
Start-Process $exe -WorkingDirectory (Split-Path $exe -Parent)
