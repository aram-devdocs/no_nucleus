# Playtest packet — apps/ split (host + Commander are now separate plugins)

**Big structural change:** the monolith `src/CommanderLayer` is gone. There are now FOUR plugins in `apps/`:
`Nucleus.Platform` (the host: com.nucleus.platform), `Nucleus.Commander` (com.nucleus.commander, hard-depends
on the platform), `Nucleus.Build`, `Nucleus.Squad`. The platform ships the shared `Nucleus.*` libs once; the
mods ship only their own DLL. The CMD button now goes through the host registry (like BLD/SQD); the map
pan-suppress is global. Build + all 7 gate layers green; deployed (stale CommanderLayer folder removed).

**One run auto-verifies everything.** `pwsh scripts/run.ps1`, open the map, then:
`pwsh scripts/audit.ps1 -LogPath .sandbox/game/BepInEx/LogOutput.log`

Expect in the log / audit:
- `Nucleus Platform loaded.` and `Nucleus Commander loaded.` (+ Build/Squad load lines).
- Four `Patched:` lines (MainMenuBadge / DynamicMapUpdateTick / VirtualMFD from Platform; AircraftTasking from Commander).
- `CommanderRuntime first Tick — driver alive.`
- `[NUCLEUS:SELFTEST] PASS` for host-tick-alive, mods-registered, game-services-readable, bezel-buttons-attached
  (and build-mod-loaded / squad-mod-loaded).
- `[NUCLEUS:METRIC] bezelButtons=3` (CMD + BLD + SQD).
- No exceptions.

**Eyeball:** main menu shows a "MODS" button (lists Commander/Build/Squad with toggles); opening the map shows
**CMD**, **BLD**, **SQD** bezel buttons; clicking CMD opens the Commander panel.

Return: paste the log (or the audit output) into `playtests/results/P-apps-split.md`. Note any exception, and
whether CMD/BLD/SQD appear and the MODS menu lists the three mods.
