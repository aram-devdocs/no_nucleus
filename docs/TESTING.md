# Testing Nucleus

Two layers: **headless gates** (run anywhere, no game) and **in-game playtests** (need the game; produce
the structured feedback in [TESTING-WORKSHEET.md](TESTING-WORKSHEET.md)).

## Headless gates (no game required)

```pwsh
pwsh scripts/check.ps1     # fast: build (warnings-as-errors) + Core unit + architecture rules
pwsh scripts/audit.ps1     # full dashboard -> artifacts/audit-summary.json
```

`audit.ps1` runs every layer and prints a PASS/FAIL dashboard, exit-coded for hooks/CI:

| Layer | What it proves | Needs game DLLs? |
|-------|----------------|------------------|
| build | whole solution compiles, 0 warnings | only the plugin/contract bits do |
| unit-core | 118 pure-logic tests | no |
| arch | dependency-graph rules (pure libs Unity-free, no app→app, DAG) | no |
| sim | 14 campaign-brain e2e invariants (deterministic, war progresses, phases advance) | no |
| contract | every game member the mod uses exists (Mono.Cecil vs Assembly-CSharp) | yes |
| integration | host mod-lifecycle (register/init/tick/enable/disable) | yes (loads Unity DLLs) |

Bash mirror: `bash scripts/check.sh`. The pure layers (unit/arch/sim) also run in cloud CI; the
game-coupled layers (contract/integration) run only where `lib/Assembly-CSharp.dll` is present.

## Build + deploy to the sandbox

```pwsh
pwsh scripts/run.ps1       # build the plugin -> deploy to .sandbox -> launch Nuclear Option
```

The build deploys each plugin to its own folder under `.sandbox/game/BepInEx/plugins/` —
`Nucleus.Platform/` (the host, which ships the shared `Nucleus.*.dll` libraries once),
`Nucleus.Commander/`, `Nucleus.Build/`, `Nucleus.Squad/` (each just its own DLL — no skew). **Never** the
Steam install — always the gitignored `.sandbox/`. Missions are copied separately to
`%AppData%\LocalLow\Shockfront\NuclearOption\Missions\`.

## In-game playtest (manual)

1. Launch (`scripts/run.ps1`) and confirm the main-menu **"Nucleus loaded - v…"** badge plus the **MODS**
   button.
2. Click **MODS** → the loader panel lists every mod (Commander/Build/Squad) with a per-mod **ON/OFF**
   toggle (state persists to BepInEx config under `Mods.<id>.Enabled`). Toggle one off/on.
3. Load the **Commander Debug** mission; open the map. Each enabled mod has its own bezel button
   (**CMD**/**BLD**/**SQD**).
4. **CMD** → the Commander panel opens and updates (proves the host-driven tick reaches the mod). Set
   **mode** OFF / MANUAL / ASSISTED / AUTO. In AUTO, the commander generates objectives, forms squads, and
   buys convoys; watch ~30–60s.
5. **Place a manual order** (arm + click map) — units get tasked.
6. Read the BepInEx log at `.sandbox/game/BepInEx/LogOutput.log` — confirm the `Patched:` lines, the
   runtime "first Tick" line, the `[NUCLEUS:SELFTEST]` lines (`loader-ui-built`, `build-mod-loaded`,
   `squad-mod-loaded`, `bezel-buttons-attached`), and **no exceptions**. Then run
   `pwsh scripts/audit.ps1 -LogPath .sandbox/game/BepInEx/LogOutput.log` for a mechanical verdict.

Record results in [TESTING-WORKSHEET.md](TESTING-WORKSHEET.md) (or the matching `playtests/<id>.md` packet),
and paste the log back so the headless log-audit (when present) can verify it mechanically.

## When you change game-facing code

If you touch reflection targets / game members, regenerate the typed SDK and re-run the contract test:

```pwsh
dotnet run --project tools/Nucleus.CodeGen -c Release
pwsh scripts/audit.ps1
```
