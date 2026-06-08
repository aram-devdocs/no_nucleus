<#
.SYNOPSIS
  Publish (or update) the "Nucleus Dynamic Warfare" Steam Workshop item via steamcmd.

  Stages the mission folder + the 256x256 preview image into a content dir, fills the
  scripts/workshop/workshop_build_item.vdf template from params/env, locates (or downloads)
  steamcmd, then runs:

      steamcmd +login <SteamUser> +workshop_build_item <abs vdf> +quit

  NO-OP (clean exit 0) when the Workshop item id or Steam login is absent, so CI stays
  green before the publish secrets are configured.

  FIRST PUBLISH: do it IN-GAME (Nuclear Option mission browser -> publish to Workshop) to
  mint the published-file id, then set STEAM_WORKSHOP_ITEM_ID to that id so this script
  updates the existing item instead of creating a duplicate.

.PARAMETER SteamUser
  Steam account name used to log in. Defaults to $env:STEAM_USER. steamcmd handles the
  password / Steam Guard cache itself (run it once interactively to cache credentials, or
  provide STEAM_PASSWORD + cached sentry on the CI host).

.PARAMETER PublishedFileId
  Existing Workshop item id to update. Defaults to $env:STEAM_WORKSHOP_ITEM_ID.

.PARAMETER MissionPath
  Mission folder to publish. Defaults to "missions/Nucleus Dynamic Warfare".

.PARAMETER PreviewFile
  256x256 preview image. Defaults to "assets/branding/nucleus-256.png".

.PARAMETER ChangeNote
  Workshop change note for this update. Defaults to $env:STEAM_WORKSHOP_CHANGENOTE or a stamp.

.PARAMETER SteamCmd
  Path to steamcmd(.exe). If omitted, uses $env:STEAMCMD, then PATH, then downloads a local
  copy into ./artifacts/steamcmd.
#>
[CmdletBinding()]
param(
  [string]$SteamUser       = $env:STEAM_USER,
  [string]$PublishedFileId = $env:STEAM_WORKSHOP_ITEM_ID,
  [string]$MissionPath,
  [string]$PreviewFile,
  [string]$ChangeNote,
  [string]$SteamCmd        = $env:STEAMCMD
)
$ErrorActionPreference = 'Stop'

$repo  = Split-Path $PSScriptRoot -Parent
$AppId = '2168680'

if (-not $MissionPath) { $MissionPath = Join-Path $repo 'missions\Nucleus Dynamic Warfare' }
if (-not $PreviewFile) { $PreviewFile = Join-Path $repo 'assets\branding\nucleus-256.png' }
if (-not $ChangeNote)  { $ChangeNote  = $env:STEAM_WORKSHOP_CHANGENOTE }
if (-not $ChangeNote)  { $ChangeNote  = "Automated publish $(Get-Date -Format 'yyyy-MM-dd HH:mm') UTC" }

# ---- NO-OP guard: stay green before secrets exist ---------------------------------------
$missing = @()
if (-not $SteamUser)       { $missing += 'STEAM_USER / -SteamUser' }
if (-not $PublishedFileId) { $missing += 'STEAM_WORKSHOP_ITEM_ID / -PublishedFileId' }
if ($missing.Count -gt 0) {
  Write-Host "[workshop] NO-OP: missing $($missing -join ' and '); skipping publish." -ForegroundColor Yellow
  Write-Host "[workshop] First publish must be done IN-GAME to mint the Workshop item id, then set STEAM_WORKSHOP_ITEM_ID." -ForegroundColor Yellow
  exit 0
}

# ---- Validate inputs --------------------------------------------------------------------
if (-not (Test-Path $MissionPath)) { throw "Mission folder not found: $MissionPath" }
if (-not (Test-Path $PreviewFile)) {
  throw "Preview image not found: $PreviewFile (the branding agent produces assets/branding/nucleus-256.png)."
}

# ---- Stage content ----------------------------------------------------------------------
$stage   = Join-Path $repo 'artifacts\workshop'
$content = Join-Path $stage 'content'
if (Test-Path $content) { Remove-Item -Recurse -Force $content }
New-Item -ItemType Directory -Force $content | Out-Null

$missionName = Split-Path $MissionPath -Leaf
$missionDest = Join-Path $content $missionName
Copy-Item -Recurse $MissionPath $missionDest
Write-Host "[workshop] staged mission '$missionName' -> $content" -ForegroundColor Cyan

$previewStaged = Join-Path $stage 'preview.png'
Copy-Item $PreviewFile $previewStaged -Force

# ---- Fill the vdf template --------------------------------------------------------------
$template = Join-Path $PSScriptRoot 'workshop\workshop_build_item.vdf'
if (-not (Test-Path $template)) { throw "vdf template not found: $template" }
$vdf = Get-Content -Raw $template
$vdf = $vdf.Replace('{PUBLISHEDFILEID}', $PublishedFileId)
$vdf = $vdf.Replace('{CONTENTFOLDER}',   $content.Replace('\', '\\'))
$vdf = $vdf.Replace('{PREVIEWFILE}',     $previewStaged.Replace('\', '\\'))
$vdf = $vdf.Replace('{CHANGENOTE}',      $ChangeNote.Replace('"', '\"'))

$vdfOut = Join-Path $stage 'workshop_build_item.vdf'
Set-Content -Path $vdfOut -Value $vdf -Encoding UTF8
Write-Host "[workshop] wrote descriptor -> $vdfOut (appid=$AppId, item=$PublishedFileId)" -ForegroundColor Cyan

# ---- Locate / download steamcmd ---------------------------------------------------------
function Resolve-SteamCmd {
  param([string]$Hint)
  if ($Hint -and (Test-Path $Hint)) { return (Resolve-Path $Hint).Path }
  $onPath = Get-Command steamcmd -ErrorAction SilentlyContinue
  if ($onPath) { return $onPath.Source }

  $dir = Join-Path $repo 'artifacts\steamcmd'
  $exe = Join-Path $dir 'steamcmd.exe'
  if (Test-Path $exe) { return $exe }

  Write-Host "[workshop] steamcmd not found; downloading to $dir ..." -ForegroundColor Cyan
  New-Item -ItemType Directory -Force $dir | Out-Null
  $zip = Join-Path $dir 'steamcmd.zip'
  Invoke-WebRequest -Uri 'https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip' -OutFile $zip
  Expand-Archive -Path $zip -DestinationPath $dir -Force
  Remove-Item $zip -Force
  if (-not (Test-Path $exe)) { throw "steamcmd download/extract failed (no $exe)." }
  return $exe
}
$steam = Resolve-SteamCmd -Hint $SteamCmd
Write-Host "[workshop] using steamcmd: $steam" -ForegroundColor Cyan

# ---- Publish ----------------------------------------------------------------------------
# steamcmd reads the password from cached credentials / Steam Guard sentry on this host, or
# from the STEAM_PASSWORD env var if you wire it into the login line. We pass user only so the
# password never lands in the process command line / CI logs.
$login = @($SteamUser)
if ($env:STEAM_PASSWORD) { $login += $env:STEAM_PASSWORD }

Write-Host "[workshop] running workshop_build_item..." -ForegroundColor Cyan
& $steam +login @login +workshop_build_item $vdfOut +quit
if ($LASTEXITCODE -ne 0) { throw "steamcmd exited $LASTEXITCODE (workshop publish failed)." }

Write-Host "[workshop] published/updated item $PublishedFileId for app $AppId." -ForegroundColor Green
