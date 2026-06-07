<#
.SYNOPSIS
  VISUAL test harness: build+deploy, arm the autoload trigger (load mission + join faction) AND the autoshot
  trigger (drive the UI + capture screenshots), launch the game, wait for the shots-complete marker (or an
  exception/timeout), kill the game, copy the PNGs into artifacts/screenshots/<timestamp>/, and print the shot
  manifest. Lets a dev (or an autonomous loop) actually SEE the in-mission UI — not just "it compiled / loaded".

  -Mission    : mission to auto-load (default "Nucleus Dynamic Warfare").
  -Faction    : faction to join so the cockpit + commander UI are live (default "Boscali"; "" = spectate).
  -TimeoutSec : how long to wait for shots-complete (default 200).
  -NoBuild    : skip build/deploy (shoot the already-deployed build).
  -Tag        : label for the output folder (default = timestamp).
#>
[CmdletBinding()]
param(
    [string]$Mission = "Nucleus Dynamic Warfare",
    [string]$Faction = "Boscali",
    [int]$TimeoutSec = 200,
    [switch]$NoBuild,
    [string]$Tag = ""
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$exe  = Join-Path $repo '.sandbox\game\NuclearOption.exe'
$log  = Join-Path $repo '.sandbox\game\BepInEx\LogOutput.log'
$autoload = Join-Path $repo '.sandbox\game\nucleus-autoload.txt'
$autoshot = Join-Path $repo '.sandbox\game\nucleus-autoshot.txt'
$shotsDir = Join-Path $repo '.sandbox\game\nucleus-shots'
if (-not (Test-Path $exe)) { throw "Sandbox exe not found at $exe (run scripts/setup-sandbox.ps1)." }

# Kill any prior game FIRST so the build can overwrite the (otherwise locked) plugin DLLs.
Get-Process NuclearOption -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

if (-not $NoBuild) {
    Write-Host "[visual] build + deploy..." -ForegroundColor Cyan
    dotnet build (Join-Path $repo 'Nucleus.sln') -c Release -p:TreatWarningsAsErrors=true | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "build failed" }
}
if (Test-Path $log) { Clear-Content $log -ErrorAction SilentlyContinue }
if (Test-Path $shotsDir) { Remove-Item (Join-Path $shotsDir '*') -Force -ErrorAction SilentlyContinue }

$triggerValue = if ($Faction) { "$Mission|$Faction" } else { $Mission }
Set-Content -Path $autoload -Value $triggerValue -Encoding utf8   # load mission (+ join faction)
Set-Content -Path $autoshot -Value "1" -Encoding utf8            # arm the screenshot driver

Write-Host "[visual] launching (mission '$Mission' faction '$Faction', timeout ${TimeoutSec}s)..." -ForegroundColor Cyan
# Force a 1920x1080 window: the mod's MFD panels are FIXED pixel size, so a smaller render target makes them
# occupy far more of the frame -> legible after the screenshot is downscaled for review.
$gameArgs = @('-screen-width','1920','-screen-height','1080','-screen-fullscreen','0')
$proc = Start-Process $exe -WorkingDirectory (Split-Path $exe -Parent) -ArgumentList $gameArgs -PassThru

$deadline = (Get-Date).AddSeconds($TimeoutSec)
$hit = $false; $fail = $false
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 3
    if ($proc.HasExited) { break }
    if (Test-Path $log) {
        $text = Get-Content $log -Raw -ErrorAction SilentlyContinue
        if ($text -match 'shots-complete') { $hit = $true; break }
        if ($text -match 'SELFTEST\] FAIL (shots|mission-autoload|join)|NullReferenceException|MissingMethodException|MissingFieldException') { $fail = $true; break }
    }
}

Write-Host "[visual] stopping game..." -ForegroundColor Cyan
Get-Process NuclearOption -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Remove-Item $autoload -ErrorAction SilentlyContinue
Remove-Item $autoshot -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

# Copy captured PNGs into a tagged artifacts folder for review.
if (-not $Tag) { $Tag = (Get-Date -Format 'yyyyMMdd-HHmmss') }
$outDir = Join-Path $repo "artifacts\screenshots\$Tag"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$copied = @()
Add-Type -AssemblyName System.Drawing
# Crop a normalized region (fractions 0..1) of a PNG into a new file, for legible review of small UI.
function Save-Crop($srcPath, $dstPath, $fx, $fy, $fw, $fh) {
    try {
        $bmp = [System.Drawing.Bitmap]::FromFile($srcPath)
        $x = [int]($bmp.Width * $fx); $y = [int]($bmp.Height * $fy)
        $w = [int]($bmp.Width * $fw); $h = [int]($bmp.Height * $fh)
        $rect = New-Object System.Drawing.Rectangle $x, $y, $w, $h
        $crop = $bmp.Clone($rect, $bmp.PixelFormat)
        $crop.Save($dstPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $crop.Dispose(); $bmp.Dispose()
        return $true
    } catch { return $false }
}
if (Test-Path $shotsDir) {
    Get-ChildItem (Join-Path $shotsDir '*.png') -ErrorAction SilentlyContinue | ForEach-Object {
        $dst = Join-Path $outDir $_.Name
        Copy-Item $_.FullName $dst -Force
        $copied += $dst
        $base = [System.IO.Path]::GetFileNameWithoutExtension($_.Name)
        # Panel shots (cmd/bld/sqd/war): zoom the bottom-left where the MFD panel renders.
        if ($base -match 'cmd|bld|sqd|war') {
            Save-Crop $_.FullName (Join-Path $outDir "$base-panel.png") 0.0 0.30 0.42 0.70 | Out-Null
        }
        # Map shots: zoom the centre where the DynamicMap + Nucleus overlay render.
        if ($base -match 'map|cmd|bld|sqd|war') {
            Save-Crop $_.FullName (Join-Path $outDir "$base-map.png") 0.34 0.0 0.40 1.0 | Out-Null
        }
    }
}

Write-Host "`n=== shot markers ===" -ForegroundColor Yellow
Select-String -Path $log -Pattern 'NUCLEUS:SHOT|shots-complete|joined-faction|mission-autoloaded|Exception' -ErrorAction SilentlyContinue | ForEach-Object { $_.Line }

Write-Host "`n=== screenshots ($($copied.Count)) -> $outDir ===" -ForegroundColor Yellow
$copied | ForEach-Object { Write-Host "  $_" }

Write-Host ""
if ($hit) { Write-Host "[visual] PASS - shots-complete, $($copied.Count) screenshots captured." -ForegroundColor Green; exit 0 }
elseif ($fail) { Write-Host "[visual] FAIL - error/exception (see markers above)." -ForegroundColor Red; exit 1 }
else { Write-Host "[visual] INCONCLUSIVE - shots-complete not seen within ${TimeoutSec}s ($($copied.Count) shots so far)." -ForegroundColor Yellow; exit 2 }
