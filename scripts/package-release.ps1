<#
.SYNOPSIS
  Assemble a GitHub release for the Nucleus mod (Nuclear Option, Steam AppId 2168680). Builds the plugins in
  Release, publishes the installer as a single-file self-contained win-x64 exe, and zips everything a player
  needs into artifacts/release/Nucleus-v<Version>.zip (plus a loose Nucleus.Installer.exe next to it).

.PARAMETER Version
  Release version string. Defaults to the <Version> in libs/Directory.Build.props (single source of truth).

.PARAMETER GameDir
  Path to the Nuclear Option install, which provides the game DLLs the plugins compile against. Auto-detected
  from the Steam registry + libraryfolders.vdf when omitted (same detection as scripts/setup-sandbox.ps1).
#>
[CmdletBinding()]
param(
    [string]$Version,
    [string]$GameDir
)

$ErrorActionPreference = 'Stop'
$AppId = '2168680'
$repo  = Split-Path $PSScriptRoot -Parent

function Info($m) { Write-Host "[pack] $m" -ForegroundColor Cyan }
function Warn($m) { Write-Host "[pack] $m" -ForegroundColor Yellow }

# --- 0. Resolve the version (libs/Directory.Build.props is the single source of truth) -----------
if (-not $Version) {
    $propsPath = Join-Path $repo 'libs\Directory.Build.props'
    if (-not (Test-Path $propsPath)) { throw "Version source not found: $propsPath" }
    [xml]$props = Get-Content $propsPath
    $node = $props.SelectSingleNode('//Version')
    if (-not $node -or -not $node.InnerText.Trim()) { throw "No <Version> element in $propsPath" }
    $Version = $node.InnerText.Trim()
}
Info "Version: $Version"

# --- 1. Locate the game (read-only) - mirrors scripts/setup-sandbox.ps1 Find-GameDir -------------
function Find-GameDir {
    try {
        $steam = (Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction Stop).SteamPath
    } catch { throw "Steam not found in registry (HKCU:\Software\Valve\Steam)." }
    $steam = $steam -replace '/', '\'
    $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
    if (-not (Test-Path $vdf)) { throw "libraryfolders.vdf not found at $vdf" }

    $content = Get-Content $vdf -Raw
    $libPaths = [regex]::Matches($content, '"path"\s*"([^"]+)"') | ForEach-Object { $_.Groups[1].Value -replace '\\\\', '\' }
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
    foreach ($p in $libPaths) {
        $candidate = Join-Path $p 'steamapps\common\Nuclear Option'
        if (Test-Path $candidate) { return $candidate }
    }
    throw "Could not locate the 'Nuclear Option' install in any Steam library."
}

if (-not $GameDir) { $GameDir = Find-GameDir }
$GameDir = (Resolve-Path $GameDir).Path
Info "Game install: $GameDir"

# --- 2. Ensure lib/ (game DLLs for the build) + .sandbox (the plugin deploy target) are ready -----
# build/Deploy.targets only deploys when $(Sandbox) (=.sandbox\game) exists; the plugins build against the
# DLLs setup-sandbox.ps1 copies into lib/. Run setup once (idempotent) if either is missing.
$lib        = Join-Path $repo 'lib'
$sandbox    = Join-Path $repo '.sandbox\game'
$pluginsOut = Join-Path $sandbox 'BepInEx\plugins'
if (-not (Test-Path (Join-Path $lib 'Assembly-CSharp.dll')) -or -not (Test-Path $sandbox)) {
    Info "Preparing lib/ + sandbox via setup-sandbox.ps1 ..."
    & (Join-Path $PSScriptRoot 'setup-sandbox.ps1') -GameDir $GameDir
} else {
    Info "lib/ and sandbox already present - skipping setup-sandbox"
}

# --- 3. Build the solution (Release). Plugins deploy to .sandbox/game/BepInEx/plugins/Nucleus.* ---
Info "Building Nucleus.sln (Release) ..."
dotnet build (Join-Path $repo 'Nucleus.sln') -c Release
if ($LASTEXITCODE -ne 0) { throw 'build failed' }

# --- 4. Publish the installer as a single-file, self-contained win-x64 exe -----------------------
$installerProj = Join-Path $repo 'tools\Nucleus.Installer\Nucleus.Installer.csproj'
$publishDir    = Join-Path $repo 'artifacts\installer-publish'
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
Info "Publishing Nucleus.Installer (single-file, self-contained, win-x64) ..."
dotnet publish $installerProj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'installer publish failed' }
$installerExe = Join-Path $publishDir 'Nucleus.Installer.exe'
if (-not (Test-Path $installerExe)) { throw "Published installer not found at $installerExe" }

# --- 5. Stage the release payload ----------------------------------------------------------------
$releaseDir = Join-Path $repo 'artifacts\release'
$stage      = Join-Path $releaseDir "stage-v$Version"
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force $stage | Out-Null

# 5a. The installer exe (and its branding icon, if present, so the desktop shortcut gets it).
Copy-Item $installerExe $stage -Force
Info "staged Nucleus.Installer.exe"
$icon = Join-Path $repo 'assets\branding\nucleus.ico'
if (Test-Path $icon) { Copy-Item $icon $stage -Force; Info "staged nucleus.ico" }
else { Warn "branding icon not found (assets\branding\nucleus.ico) - shortcut will use default icon" }

# 5b. The Nucleus.* plugin folders produced by the build (Platform ships the shared libs once).
if (-not (Test-Path $pluginsOut)) { throw "Plugin output not found at $pluginsOut - did the build deploy?" }
$pluginFolders = Get-ChildItem -Directory $pluginsOut | Where-Object { $_.Name -like 'Nucleus.*' }
if (-not $pluginFolders) { throw "No Nucleus.* plugin folders under $pluginsOut" }
foreach ($pf in $pluginFolders) {
    Copy-Item -Recurse $pf.FullName (Join-Path $stage $pf.Name) -Force
    Info "staged plugin: $($pf.Name)"
}

# 5c. The dynamic-warfare mission (data-only discovery vehicle), kept under missions/ so it is not
#     mistaken for a plugin folder (installer only deploys folders named 'Nucleus.*').
$missionSrc = Join-Path $repo 'missions\Nucleus Dynamic Warfare'
if (-not (Test-Path $missionSrc)) { throw "Mission folder not found: $missionSrc" }
$missionStage = Join-Path $stage 'missions'
New-Item -ItemType Directory -Force $missionStage | Out-Null
Copy-Item -Recurse $missionSrc $missionStage -Force
Info "staged mission: Nucleus Dynamic Warfare"

# 5d. Version stamp (installer's VersionStamp.Shipping reads this from the payload root).
Set-Content -Path (Join-Path $stage 'nucleus-version.txt') -Value $Version -Encoding Ascii -NoNewline
Info "staged nucleus-version.txt"

# 5e. INSTALL.txt - extract + run the installer.
$install = @"
Nucleus for Nuclear Option - EARLY ALPHA (v$Version)
====================================================

INSTALL
  1. Extract this entire zip to a folder (keep all files together).
  2. Run:  Nucleus.Installer.exe install
     The installer auto-detects your Steam copy of Nuclear Option, sets up BepInEx,
     copies the Nucleus.* plugins, and creates a "Nucleus" desktop shortcut.

PLAY
  - Launching from Steam stays VANILLA (Nucleus stays off).
  - Use the "Nucleus" desktop shortcut to launch the game WITH Nucleus.

OPTIONAL MISSION
  - The bundled "Nucleus Dynamic Warfare" mission is under the missions\ folder.
    Copy it into:
      %USERPROFILE%\AppData\LocalLow\Shockfront\NuclearOption\Missions\
    then pick it in the in-game singleplayer mission browser.

OTHER COMMANDS
  Nucleus.Installer.exe detect      (show the detected game folder)
  Nucleus.Installer.exe update      (upgrade to the latest release if newer)
  Nucleus.Installer.exe verify      (repair BepInEx / plugins)
  Nucleus.Installer.exe uninstall   (remove the Nucleus plugins)
"@
Set-Content -Path (Join-Path $stage 'INSTALL.txt') -Value $install -Encoding utf8
Info "staged INSTALL.txt"

# --- 6. Zip the payload + drop a loose installer next to it --------------------------------------
$zip = Join-Path $releaseDir "Nucleus-v$Version.zip"
if (Test-Path $zip) { Remove-Item -Force $zip }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
$looseExe = Join-Path $releaseDir 'Nucleus.Installer.exe'
Copy-Item $installerExe $looseExe -Force

# --- 7. Report -----------------------------------------------------------------------------------
Info "DONE."
Info "  zip:       $zip"
Info "  installer: $looseExe"
Info "  staged at: $stage"
