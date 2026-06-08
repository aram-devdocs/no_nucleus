<#
.SYNOPSIS
  Assemble and build the Thunderstore package for Nucleus Dynamic Warfare, then publish it
  (publish is GATED — it only runs when $env:TCLI_AUTH_TOKEN is set).

  Pipeline:
    1. Read the version of truth from libs/Directory.Build.props (<Version>).
    2. Sync that version into thunderstore/manifest.json (version_number).
    3. Stage a build dir with: manifest.json, icon.png (from assets/branding/nucleus-256.png),
       README.md, CHANGELOG.md, and plugins/<PluginName>/*.dll (the deployed Nucleus.* plugins).
    4. Generate a thunderstore.toml mirroring manifest.json and run 'tcli build'.
    5. Run 'tcli publish' ONLY if $env:TCLI_AUTH_TOKEN is set; otherwise print a gated-skip notice.

  Requires the Thunderstore CLI (tcli) on PATH:  dotnet tool install --global tcli
  Community: 'nuclear-option' (https://thunderstore.io/c/nuclear-option/).

.PARAMETER PluginsSource
  Folder containing the built plugin sub-folders (Nucleus.Platform/, Nucleus.Commander/, ...).
  Defaults to the sandbox deploy output produced by `dotnet build` (.sandbox/game/BepInEx/plugins).

.PARAMETER Repository
  Thunderstore repository base URL (default: https://thunderstore.io).

.PARAMETER Community
  Thunderstore community slug to publish into (default: nuclear-option).

.PARAMETER SkipBuild
  Skip 'tcli build' (only stage the package dir + sync version).
#>
[CmdletBinding()]
param(
    [string]$PluginsSource,
    [string]$Repository = 'https://thunderstore.io',
    [string]$Community = 'nuclear-option',
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'

$repo  = Split-Path $PSScriptRoot -Parent
$tsDir = Join-Path $repo 'thunderstore'
$stage = Join-Path $repo 'build\thunderstore-package'
$props = Join-Path $repo 'libs\Directory.Build.props'
$icon  = Join-Path $repo 'assets\branding\nucleus-256.png'
$manifestPath = Join-Path $tsDir 'manifest.json'

if (-not $PluginsSource) { $PluginsSource = Join-Path $repo '.sandbox\game\BepInEx\plugins' }

function Info($m) { Write-Host "[thunderstore] $m" -ForegroundColor Cyan }
function Warn($m) { Write-Host "[thunderstore] $m" -ForegroundColor Yellow }

# The Nucleus plugin folders that ship in the package (each is a BepInEx plugin dir of DLLs).
$pluginFolders = 'Nucleus.Platform','Nucleus.Commander','Nucleus.Warfare','Nucleus.Squad','Nucleus.Build'

# --- 1. Version of truth ----------------------------------------------------------------------
if (-not (Test-Path $props)) { throw "Version source not found: $props" }
$propsXml = [xml](Get-Content $props -Raw)
$version  = ($propsXml.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ }) | Select-Object -First 1
if (-not $version) { throw "Could not read <Version> from $props" }
$version = "$version".Trim()
Info "Version of truth: $version"

# --- 2. Sync version into manifest.json -------------------------------------------------------
if (-not (Test-Path $manifestPath)) { throw "manifest not found: $manifestPath" }
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
if ($manifest.version_number -ne $version) {
    Info "Syncing manifest.json version_number $($manifest.version_number) -> $version"
    $manifest.version_number = $version
    # Re-serialize with stable key order (name, version_number, website_url, description, dependencies).
    $ordered = [ordered]@{
        name           = $manifest.name
        version_number = $manifest.version_number
        website_url    = $manifest.website_url
        description    = $manifest.description
        dependencies   = @($manifest.dependencies)
    }
    ($ordered | ConvertTo-Json -Depth 5) | Set-Content -Path $manifestPath -Encoding UTF8
} else {
    Info "manifest.json already at $version"
}

# --- 3. Stage the package dir -----------------------------------------------------------------
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force $stage | Out-Null

Copy-Item $manifestPath (Join-Path $stage 'manifest.json') -Force
Copy-Item (Join-Path $tsDir 'README.md')    (Join-Path $stage 'README.md') -Force
Copy-Item (Join-Path $tsDir 'CHANGELOG.md') (Join-Path $stage 'CHANGELOG.md') -Force

if (Test-Path $icon) {
    Copy-Item $icon (Join-Path $stage 'icon.png') -Force
    Info "icon: $icon -> icon.png"
} else {
    Warn "icon not found at $icon (the branding agent produces nucleus-256.png). Build will fail without a 256x256 icon.png."
}

$pluginsOut = Join-Path $stage 'plugins'
New-Item -ItemType Directory -Force $pluginsOut | Out-Null
$copied = 0
foreach ($pf in $pluginFolders) {
    $src = Join-Path $PluginsSource $pf
    if (Test-Path $src) {
        Copy-Item $src $pluginsOut -Recurse -Force
        $n = (Get-ChildItem (Join-Path $pluginsOut $pf) -Filter *.dll -Recurse).Count
        Info "staged plugin $pf ($n dll)"
        $copied += $n
    } else {
        Warn "missing plugin folder (skipped): $src"
    }
}
if ($copied -eq 0) {
    Warn "No plugin DLLs staged from $PluginsSource. Build a Release first (dotnet build Nucleus.sln -c Release) or pass -PluginsSource."
}

# --- 4. Generate thunderstore.toml (tcli config) + build --------------------------------------
# tcli reads thunderstore.toml; we mirror the authored manifest.json fields into it so manifest.json
# stays the single authored source. [build.copy] reproduces the BepInEx/plugins layout in the zip.
$ns = 'Nucleus'   # Thunderstore namespace (team). Adjust to your team slug if different.
$depLines = ($manifest.dependencies | ForEach-Object {
    $idx = $_.LastIndexOf('-')
    $pkg = $_.Substring(0, $idx)
    $ver = $_.Substring($idx + 1)
    "`"$pkg`" = `"$ver`""
}) -join "`n"

$toml = @"
[config]
schemaVersion = "0.0.1"

[package]
namespace = "$ns"
name = "$($manifest.name)"
versionNumber = "$version"
description = "$($manifest.description)"
websiteUrl = "$($manifest.website_url)"
containsNsfwContent = false

[package.dependencies]
$depLines

[build]
icon = "./icon.png"
readme = "./README.md"
outdir = "./dist"

[[build.copy]]
source = "./manifest.json"
target = "manifest.json"

[[build.copy]]
source = "./CHANGELOG.md"
target = "CHANGELOG.md"

[[build.copy]]
source = "./plugins"
target = "BepInEx/plugins"

[publish]
repository = "$Repository"
communities = ["$Community"]
"@
$tomlPath = Join-Path $stage 'thunderstore.toml'
Set-Content -Path $tomlPath -Value $toml -Encoding UTF8
Info "wrote $tomlPath"

if ($SkipBuild) { Info "SkipBuild set — staged package only. Done."; return }

$tcli = Get-Command tcli -ErrorAction SilentlyContinue
if (-not $tcli) {
    throw "tcli not found on PATH. Install it: dotnet tool install --global tcli"
}

Info "tcli build (config: $tomlPath)"
# NOTE: tcli flags can vary by version — verify against your installed tcli (`tcli build --help`).
& tcli build --config-path $tomlPath --package-version $version
if ($LASTEXITCODE -ne 0) { throw "tcli build failed (exit $LASTEXITCODE)" }
Info "build artifact written under $(Join-Path $stage 'dist')"

# --- 5. Publish (GATED) -----------------------------------------------------------------------
if ([string]::IsNullOrWhiteSpace($env:TCLI_AUTH_TOKEN)) {
    Warn "PUBLISH SKIPPED — TCLI_AUTH_TOKEN is not set."
    Warn "To publish: set `$env:TCLI_AUTH_TOKEN to a Thunderstore service-account token, then re-run."
    return
}

Info "tcli publish -> $Repository (community: $Community)"
& tcli publish --config-path $tomlPath --token $env:TCLI_AUTH_TOKEN --repository $Repository
if ($LASTEXITCODE -ne 0) { throw "tcli publish failed (exit $LASTEXITCODE)" }
Info "PUBLISHED $($manifest.name) $version"
