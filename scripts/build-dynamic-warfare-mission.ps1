<#
.SYNOPSIS
  Build the "Nucleus Dynamic Warfare" mission by FORKING the game's shipped Escalation mission (an enormous
  attrition war with all airbases active - the ideal base for the Nucleus attrition game mode). Escalation is
  the game's IP and ships as a Unity Resources TextAsset, so it is NOT redistributed in this repo; this script
  produces the fork LOCALLY from your own game files. It swaps in the Nucleus description and writes the result
  to the repo working tree (gitignored) and installs it into your user Missions folder.

  Prerequisite: run 'make export-missions' first to dump the built-in missions to artifacts/missions-export.

  -Source : path to an Escalation.json export (default: artifacts/missions-export/Escalation.json).
#>
[CmdletBinding()]
param([string]$Source)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (-not $Source) { $Source = Join-Path $repo 'artifacts\missions-export\Escalation.json' }
if (-not (Test-Path $Source)) {
    throw "Escalation export not found at $Source. Run 'make export-missions' first (it boots the game and dumps the built-in missions)."
}

# The Nucleus attrition-war description (our content) replaces Escalation's. The first line is an EARLY ALPHA
# banner: this Description is the most prominent Nucleus-authored text shown in the mission browser/briefing,
# so it carries the [EARLY ALPHA] label (the mission's displayed name is the folder name - see $name below).
$desc = '[EARLY ALPHA] NUCLEUS DYNAMIC WARFARE - early-alpha community mod in active development; expect rough edges, bugs and breaking changes. Nucleus Dynamic Warfare - the attrition war game mode, forked from Escalation (all airbases active, full combined-arms). YOUR objectives drive the war, not the game''s. Open the map and use the bezel buttons: CMD (drop objectives - pick a kind, click the map, select a marker to edit in place; plus the two command toggles AI COMMANDER and AI AUTO-FILL), BLD (buy convoys / build at base), SQD (manage squads), WAR (the ATTRITION scoreboard - both factions'' score, funds and losses, plus operations and the battle feed). Win by attrition: lose units/bases or spend on reinforcement and your score falls (spending bleeds faster the more bases you''ve lost); drive the enemy to zero to win. Turn AI COMMANDER off to create objectives yourself or on to let the AI run your side - set per side, so either faction can be human or AI. The persistent two-faction war saves and resumes across sessions. INSTALL: GitHub-release installer (Nucleus.Installer.exe - recommended), or Thunderstore (search Nucleus), or build from source (clone the repo and run make mission). Unofficial community mod - not affiliated with or endorsed by Shockfront Studios.'

$raw = [System.IO.File]::ReadAllText($Source)
$pattern = '"description":\s*"[^"]*"'
$replacement = '"description": "' + $desc + '"'
$out = [System.Text.RegularExpressions.Regex]::Replace(
    $raw, $pattern,
    [System.Text.RegularExpressions.MatchEvaluator]{ param($m) $replacement },
    [System.Text.RegularExpressions.RegexOptions]::Singleline, [System.TimeSpan]::FromSeconds(10))
if ($out -eq $raw) { throw "Could not find the description field to replace in $Source." }

# Neuter the game's scripted objectives so ONLY Nucleus objectives drive this mode (the north-star: with no
# scripted objectives the forces idle, and OUR objectives become what is dynamic). Replace the top-level
# objectives block (the LAST "objectives" key - factions' own objectives come earlier in the file) with a
# single inert "Mission Start" None objective and no outcomes. The forked Escalation ships 62 scripted
# objectives ("Capture Airbase" etc.) that otherwise render on the map and compete with ours.
$idx = $out.LastIndexOf('"objectives"')
if ($idx -lt 0) { throw "Could not find the top-level objectives block to neuter." }
$inert = '"objectives": { "Objectives": [ { "UniqueName": "Mission Start", "Faction": "", "DisplayName": "", "Hidden": true, "Type": 0, "TypeName": "None", "Data": [], "Outcomes": [] } ], "Outcomes": [] }' + "`n}"
$out = $out.Substring(0, $idx) + $inert

# Sanity: the result must still parse as JSON.
try { $null = $out | ConvertFrom-Json } catch { throw "Forked mission is not valid JSON: $($_.Exception.Message)" }

# The mission's in-game NAME is the folder/file name (Mission.Name is [NonSerialized] - the game derives it
# from the directory, not from any JSON field). The platform gates the war mode on an EXACT match to this
# string (WarSetupController.ModeMission), so the [EARLY ALPHA] label lives in $desc above, NOT here -
# renaming the folder would stop the mode's setup screen from ever appearing.
$name = 'Nucleus Dynamic Warfare'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

# 1) repo working tree (gitignored - game IP is never committed).
$repoDir = Join-Path $repo "missions\$name"
New-Item -ItemType Directory -Force $repoDir | Out-Null
[System.IO.File]::WriteAllText((Join-Path $repoDir "$name.json"), $out, $utf8NoBom)

# 2) user Missions folder (so it shows up in the singleplayer browser).
$userDir = Join-Path ([Environment]::GetFolderPath('LocalApplicationData') + 'Low') "Shockfront\NuclearOption\Missions\$name"
# LocalApplicationData maps to AppData\Local; the game uses AppData\LocalLow.
$userDir = Join-Path ([Environment]::GetEnvironmentVariable('USERPROFILE')) "AppData\LocalLow\Shockfront\NuclearOption\Missions\$name"
New-Item -ItemType Directory -Force $userDir | Out-Null
[System.IO.File]::WriteAllText((Join-Path $userDir "$name.json"), $out, $utf8NoBom)

Write-Host "[mission] forked Escalation -> '$name' ($($out.Length) chars)" -ForegroundColor Green
Write-Host "[mission] repo (gitignored): $repoDir" -ForegroundColor DarkGray
Write-Host "[mission] installed:         $userDir" -ForegroundColor DarkGray
