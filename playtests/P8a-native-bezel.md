# Playtest P8a — native MFD bezel buttons (replaces slot-hijack)

**What changed:** the map bezel buttons (CMD/BLD/SQD/WAR) now attach by **cloning one of the game's own
bezel buttons** (native style + placed by the game's own layout group) and appending it to the MFD's button
list — instead of the old reflection hunt for a "blank" slot (which found nothing → no buttons, the bug you
hit). The custom-Canvas "MODS" main-menu overlay is **removed** (native menu entry comes next); per-mod
on/off is temporarily in the BepInEx ConfigurationManager (`Mods.<id>.Enabled`).

## Run
1. `pwsh scripts/run.ps1`
2. Load a mission **with an aircraft** (so the in-cockpit MFD exists) — e.g. Commander Debug.
3. **Maximize the map** (the in-cockpit MFD map — the same action you did before).
4. Look at the bezel: you should see **CMD, BLD, SQD, WAR** buttons in the game's native style. Click CMD —
   the Commander panel should toggle.
5. Exit, then run:
   `pwsh scripts/audit.ps1 -LogPath .sandbox/game/BepInEx/LogOutput.log`

## What the log tells us (paste it back either way)
- `[NUCLEUS:METRIC] mfdMaximizeHook=1 rightButtons=<n> chosenButtons=<n>` — confirms the map-maximize hook
  fired and how many native bezel buttons it saw. **If this line is ABSENT**, the hook isn't firing in your
  flow (then I attach on a different trigger) — that's the key datapoint.
- `[NUCLEUS:SELFTEST] PASS bezel-buttons-attached` + `[NUCLEUS:METRIC] bezelButtons=4` — buttons added.
- Note visually: do CMD/BLD/SQD/WAR appear, in native style, correctly placed (not overlapping)? Does CMD open the panel?

## Why this is the foundation
This proves the "instantiate the game's own widget" pattern. Once the native bezel button is confirmed, the
same pattern carries the native main-menu entry and moving each mod panel into a real MFD screen (displayPanel).
