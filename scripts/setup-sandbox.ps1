<#
.SYNOPSIS
  Build the gitignored .sandbox/ mirror of the Nuclear Option install, install BepInEx 5 (x64 Mono) +
  ConfigurationManager, and publicize the game assembly into lib/. NEVER writes to the Steam install.

.PARAMETER GameDir
  Path to the Nuclear Option install. Auto-detected from the Steam registry + libraryfolders.vdf if omitted.

.PARAMETER Force
  Recreate the sandbox even if it already exists.
#>
[CmdletBinding()]
param(
    [string]$GameDir,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$AppId   = '2168680'
$repo    = Split-Path $PSScriptRoot -Parent
$sandbox = Join-Path $repo '.sandbox\game'
$lib     = Join-Path $repo 'lib'

function Info($m) { Write-Host "[setup] $m" -ForegroundColor Cyan }
function Warn($m) { Write-Host "[setup] $m" -ForegroundColor Yellow }

# --- 0. Locate the game (read-only) --------------------------------------------------------------
function Find-GameDir {
    try {
        $steam = (Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction Stop).SteamPath
    } catch { throw "Steam not found in registry (HKCU:\Software\Valve\Steam)." }
    $steam = $steam -replace '/', '\'
    $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
    if (-not (Test-Path $vdf)) { throw "libraryfolders.vdf not found at $vdf" }

    # Parse the VDF into library blocks; find the one whose "apps" list contains our AppId.
    $content = Get-Content $vdf -Raw
    $libPaths = [regex]::Matches($content, '"path"\s*"([^"]+)"') | ForEach-Object { $_.Groups[1].Value -replace '\\\\', '\' }
    # Split into per-library segments to associate apps with the right path.
    $blocks = [regex]::Split($content, '(?="path")')
    foreach ($b in $blocks) {
        $pm = [regex]::Match($b, '"path"\s*"([^"]+)"')
        if (-not $pm.Success) { continue }
        if ($b -match "`"$AppId`"") {
            $p = $pm.Groups[1].Value -replace '\\\\', '\'
            $candidate = Join-Path $p 'steamapps\common\Nuclear Option'
            if (Test-Path $candidate) { return $candidate }
        }
    }
    # Fallback: scan all libraries for the install folder.
    foreach ($p in $libPaths) {
        $candidate = Join-Path $p 'steamapps\common\Nuclear Option'
        if (Test-Path $candidate) { return $candidate }
    }
    throw "Could not locate the 'Nuclear Option' install in any Steam library."
}

if (-not $GameDir) { $GameDir = Find-GameDir }
$GameDir = (Resolve-Path $GameDir).Path
$gameData = Join-Path $GameDir 'NuclearOption_Data'
$gameManaged = Join-Path $gameData 'Managed'
if (-not (Test-Path (Join-Path $gameManaged 'Assembly-CSharp.dll'))) {
    throw "Assembly-CSharp.dll not found under $gameManaged - is GameDir correct?"
}
Info "Game install: $GameDir"
$hash = (Get-Content (Join-Path $GameDir 'build-hash.txt') -ErrorAction SilentlyContinue)
if ($hash) { Info "Game build-hash: $hash (pin this in README)" }

# --- 1. Prepare sandbox dirs ---------------------------------------------------------------------
if ($Force -and (Test-Path $sandbox)) {
    Warn "Force: removing existing sandbox $sandbox"
    # Remove junction first so we never recurse into the real data folder.
    $jn = Join-Path $sandbox 'NuclearOption_Data'
    if (Test-Path $jn) { cmd /c rmdir "$jn" | Out-Null }
    Remove-Item -Recurse -Force $sandbox
}
New-Item -ItemType Directory -Force $sandbox, $lib | Out-Null

# --- 2. Copy small root files --------------------------------------------------------------------
$rootFiles = 'NuclearOption.exe','UnityPlayer.dll','UnityCrashHandler64.exe','build-hash.txt'
foreach ($f in $rootFiles) {
    $src = Join-Path $GameDir $f
    if (Test-Path $src) { Copy-Item $src $sandbox -Force; Info "copied $f" }
    else { Warn "missing root file (skipped): $f" }
}
# MonoBleedingEdge runtime is needed by the player; junction it (read-only).
$mbeSrc = Join-Path $GameDir 'MonoBleedingEdge'
$mbeDst = Join-Path $sandbox 'MonoBleedingEdge'
if ((Test-Path $mbeSrc) -and -not (Test-Path $mbeDst)) {
    cmd /c mklink /J "$mbeDst" "$mbeSrc" | Out-Null; Info "junctioned MonoBleedingEdge"
}

# --- 3. Junction the big data folder (read-only by discipline) -----------------------------------
$dataDst = Join-Path $sandbox 'NuclearOption_Data'
if (-not (Test-Path $dataDst)) {
    cmd /c mklink /J "$dataDst" "$gameData" | Out-Null
    Info "junctioned NuclearOption_Data -> $gameData"
}

# --- 4. steam_appid.txt --------------------------------------------------------------------------
Set-Content -Path (Join-Path $sandbox 'steam_appid.txt') -Value $AppId -Encoding Ascii -NoNewline
Info "wrote steam_appid.txt ($AppId)"

# --- helper: download latest GitHub release asset matching a name pattern -------------------------
function Get-LatestReleaseAsset {
    param([string]$Repo, [string]$Pattern, [string]$OutFile)
    $api = "https://api.github.com/repos/$Repo/releases/latest"
    $headers = @{ 'User-Agent' = 'commander-layer-setup' }
    $rel = Invoke-RestMethod -Uri $api -Headers $headers
    $asset = $rel.assets | Where-Object { $_.name -match $Pattern } | Select-Object -First 1
    if (-not $asset) { throw "No asset matching /$Pattern/ in latest $Repo release ($($rel.tag_name))." }
    Info "downloading $($asset.name) ($($rel.tag_name))"
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $OutFile -Headers $headers
    return $asset.name
}

# --- 5. Install BepInEx 5 x64 Mono ---------------------------------------------------------------
$bepInExMarker = Join-Path $sandbox 'winhttp.dll'
if (-not (Test-Path $bepInExMarker)) {
    $tmp = Join-Path $env:TEMP 'bepinex_no.zip'
    Get-LatestReleaseAsset -Repo 'BepInEx/BepInEx' -Pattern 'BepInEx_win_x64_5\..*\.zip$' -OutFile $tmp | Out-Null
    Expand-Archive -Path $tmp -DestinationPath $sandbox -Force
    Remove-Item $tmp -Force
    Info "BepInEx installed into sandbox"
} else { Info "BepInEx already present (winhttp.dll)" }

# --- 6. ConfigurationManager (F1 in-game config UI) ----------------------------------------------
$pluginsDir = Join-Path $sandbox 'BepInEx\plugins'
New-Item -ItemType Directory -Force $pluginsDir | Out-Null
if (-not (Get-ChildItem $pluginsDir -Recurse -Filter 'ConfigurationManager.dll' -ErrorAction SilentlyContinue)) {
    $tmp = Join-Path $env:TEMP 'configmgr.zip'
    # Match the BepInEx 5 build of ConfigurationManager.
    Get-LatestReleaseAsset -Repo 'BepInEx/BepInEx.ConfigurationManager' -Pattern '(?i)BepInEx5|BepInEx\.ConfigurationManager_.*\.zip$' -OutFile $tmp | Out-Null
    $cmExtract = Join-Path $env:TEMP 'configmgr_x'
    if (Test-Path $cmExtract) { Remove-Item -Recurse -Force $cmExtract }
    Expand-Archive -Path $tmp -DestinationPath $cmExtract -Force
    $dll = Get-ChildItem $cmExtract -Recurse -Filter 'ConfigurationManager.dll' | Select-Object -First 1
    if (-not $dll) { throw "ConfigurationManager.dll not found in the downloaded zip." }
    Copy-Item $dll.FullName $pluginsDir -Force
    Remove-Item $tmp,$cmExtract -Recurse -Force
    Info "ConfigurationManager installed"
} else { Info "ConfigurationManager already present" }

# --- 7. Copy the REAL game assembly into lib/ (unmodified; compile target for type-safety) --------
# We reference the unmodified Assembly-CSharp.dll so the C# compiler enforces accessibility against the
# game's actual types (private access = build error, caught here not at runtime). No publicizer needed.
Copy-Item (Join-Path $gameManaged 'Assembly-CSharp.dll') (Join-Path $lib 'Assembly-CSharp.dll') -Force
Info "copied real Assembly-CSharp.dll into lib/"

# --- 8. Copy reference UnityEngine DLLs into lib/ ------------------------------------------------
$unityDlls = 'UnityEngine.dll','UnityEngine.CoreModule.dll','UnityEngine.PhysicsModule.dll','UnityEngine.IMGUIModule.dll',
             'UnityEngine.InputLegacyModule.dll','UnityEngine.TextRenderingModule.dll','UnityEngine.UIModule.dll',
             'UnityEngine.UI.dll','UnityEngine.TerrainModule.dll','UnityEngine.TerrainPhysicsModule.dll',
             'Mirage.dll','Unity.TextMeshPro.dll'
foreach ($d in $unityDlls) {
    $src = Join-Path $gameManaged $d
    if (Test-Path $src) { Copy-Item $src $lib -Force }
}
Info "copied UnityEngine reference DLLs into lib/"

# Reference BepInEx's OWN core DLLs (exact runtime versions) instead of NuGet, to avoid Harmony/MonoMod
# version mismatches (e.g. NuGet HarmonyX requiring MonoMod.Backports that BepInEx 5.4.23 doesn't ship).
$core = Join-Path $sandbox 'BepInEx\core'
foreach ($d in 'BepInEx.dll','0Harmony.dll') {
    $src = Join-Path $core $d
    if (Test-Path $src) { Copy-Item $src $lib -Force } else { Warn "missing core DLL: $d" }
}
Info "copied BepInEx core reference DLLs into lib/"

Info "DONE. Next: launch once with scripts/run.ps1, set BepInEx.cfg (HideGameManagerObject=true,"
Info "Logging.Console Enabled=true), then scripts/decompile.ps1 for the Phase 1 gate."
