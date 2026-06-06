# Playtest packet — P3d: main-menu mod loader

**What's new:** the main menu now has a **MODS** button (top-left, under the "Nucleus loaded" badge). Clicking
it opens a small panel listing the registered mods (currently **Commander**) with a per-mod **ON/OFF** toggle
that enables/disables the mod at runtime via the host registry. Build-green + deployed to `.sandbox`.

**Auto-verified:** on the main menu, the host logs `[NUCLEUS:SELFTEST] PASS loader-ui-built` and
`[NUCLEUS:METRIC] loaderMods=1`. So after you run, `pwsh scripts/audit.ps1 -LogPath .sandbox/game/BepInEx/LogOutput.log`
confirms it mechanically — you mainly need to eyeball the UI.

## Steps
1. Launch (`pwsh scripts/run.ps1`). At the **main menu**:
   - ✅ a **MODS** button appears (top-left).
   - Click it → a panel lists **Commander** with an **ON** toggle.
   - Click the toggle → it flips ON↔OFF (disables/enables the Commander mod).
2. (optional) Start the mission with Commander **OFF** → the CMD panel/commander should not drive; toggle
   **ON** and it should. (Toggle currently affects the live tick; persistence across launches is a follow-up.)
3. Exit.

## Return
Paste the BepInEx log into `playtests/results/P3d-loader.md` (or just run the audit command above and paste its
output). Note PASS/FAIL for: MODS button visible, panel lists Commander, toggle flips. Screenshot of the open
panel is helpful. Any exceptions?

Known-rough (polish later): panel spacing/positioning is first-pass; enable-state isn't persisted across
launches yet.
