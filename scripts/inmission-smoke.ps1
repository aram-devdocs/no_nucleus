<#
.SYNOPSIS
  In-mission test harness: build+deploy, write the autoload trigger file, launch the game (the platform's
  MissionAutoLoader programmatically loads the named mission + emits in-mission self-test markers), wait for the
  in-mission marker (or an exception/timeout), kill the game, print the in-mission census, verdict. Lets the dev
  verify IN-MISSION behaviour (objectives, faction binding, scoreboard inputs) without a human navigating menus.

  -Mission    : mission name to auto-load (default "Nucleus Dynamic Warfare").
  -TimeoutSec : how long to wait for the in-mission marker (default 220).
  -NoBuild    : skip build/deploy (test the already-deployed build).
#>
[CmdletBinding()]
param(
    [string]$Mission = "Nucleus Dynamic Warfare",
    [string]$Faction = "",          # optional: join this side and probe player-side behaviour (objectives)
    [int]$TimeoutSec = 220,
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$exe  = Join-Path $repo '.sandbox\game\NuclearOption.exe'
$log  = Join-Path $repo '.sandbox\game\BepInEx\LogOutput.log'
$trigger = Join-Path $repo '.sandbox\game\nucleus-autoload.txt'
if (-not (Test-Path $exe)) { throw "Sandbox exe not found at $exe (run scripts/setup-sandbox.ps1)." }

# Kill any prior game FIRST so the build can overwrite the (otherwise locked) plugin DLLs.
Get-Process NuclearOption -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

if (-not $NoBuild) {
    Write-Host "[inmission] build + deploy..." -ForegroundColor Cyan
    dotnet build (Join-Path $repo 'Nucleus.sln') -c Release -p:TreatWarningsAsErrors=true | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "build failed" }
}
if (Test-Path $log) { Clear-Content $log -ErrorAction SilentlyContinue }
$triggerValue = if ($Faction) { "$Mission|$Faction" } else { $Mission }
Set-Content -Path $trigger -Value $triggerValue -Encoding utf8   # arm the auto-loader (Mission or Mission|Faction)

Write-Host "[inmission] launching (auto-loading '$Mission', timeout ${TimeoutSec}s)..." -ForegroundColor Cyan
$proc = Start-Process $exe -WorkingDirectory (Split-Path $exe -Parent) -PassThru

# With a faction we wait for the post-join probe; otherwise just for the in-mission census.
$doneMarker = if ($Faction) { 'no-phantom-objectives|phantom-objectives-on-friendlies' } else { 'inmission-units-present' }
$deadline = (Get-Date).AddSeconds($TimeoutSec)
$hit = $null; $fail = $false
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 3
    if ($proc.HasExited) { break }
    if (Test-Path $log) {
        $text = Get-Content $log -Raw -ErrorAction SilentlyContinue
        if ($text -match $doneMarker) { $hit = $true; break }
        if ($text -match 'SELFTEST\] FAIL (mission-autoload|join|probe)|NullReferenceException|MissingMethodException|MissingFieldException') { $fail = $true; break }
    }
}

Write-Host "[inmission] stopping game..." -ForegroundColor Cyan
Get-Process NuclearOption -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Remove-Item $trigger -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

Write-Host "`n=== in-mission markers ===" -ForegroundColor Yellow
Select-String -Path $log -Pattern 'auto-loader|autoload|inmission|NUCLEUS:SELFTEST|NUCLEUS:METRIC|Exception' | ForEach-Object { $_.Line }

Write-Host ""
if ($hit) { Write-Host "[inmission] PASS - reached in-mission, census emitted." -ForegroundColor Green; exit 0 }
elseif ($fail) { Write-Host "[inmission] FAIL - autoload error/exception (see markers above)." -ForegroundColor Red; exit 1 }
else { Write-Host "[inmission] INCONCLUSIVE - in-mission marker not seen within ${TimeoutSec}s." -ForegroundColor Yellow; exit 2 }
