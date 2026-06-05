# Commander

A **commander layer** mod for the game **[Nuclear Option](https://store.steampowered.com/app/2168680/Nuclear_Option/)**
(Steam App `2168680`, by Shockfront Studios). Instead of piloting every unit, you open the map, drop an
objective, and the game's own AI moves suitable units to it — *Majesty*, not an RTS.

> ⚠️ This is an unofficial, community code mod built with BepInEx + HarmonyX. It is not affiliated with or
> endorsed by Shockfront Studios.

## What it does (milestone 1)

- **Main menu badge** — confirms the mod loaded.
- **In-map CMD button** — opening the map (`M`) adds a **CMD** button to a blank MFD bezel slot. Clicking it
  opens the Commander panel (the panel is the "modal" for that button).
- **Drop an objective** — *Place Objective*, then click the map. A faction objective is registered with the
  game's mission runner; the AI assigner (`MissionPosition`) steers idle friendly ships/ground vehicles to
  it (aircraft divert toward it when not engaged).
- **Readback + overlay** — the panel lists assigned units (distance, en-route/arrived) and the map draws a
  marker plus lines to each assigned unit.

Config (arrive radius, optional arm key) lives in the in-game **F1** ConfigurationManager menu.

## How it works (the integration)

The game is Unity (Mono backend) + uGUI/TextMeshPro. The mod:
- Reads the local faction (`GameManager.GetLocalHQ/GetLocalFaction`) and units (`UnitRegistry.allUnits`).
- Registers a `CommanderObjective : NoObjective, IObjectiveWithPosition` into `MissionManager.Runner`'s
  public `ActiveObjectives`/`activeByFaction` lists; the stock AI consumes it.
- Drives its per-frame loop from a Harmony postfix on `DynamicMap.Update` (this game does not pump a
  MonoBehaviour `Update` on mod-created objects), and attaches the CMD button from a postfix on
  `VirtualMFD.VirtualMFD_onMapMaximized`.

### Type-safety against the game

The plugin **compiles against the real, unmodified `Assembly-CSharp.dll`** (not a publicized copy), so the
C# compiler enforces the game's actual accessibility — any private access is a build error caught locally,
never a runtime `FieldAccessException`. The few genuinely-private members we need (the `VirtualMFD` bezel
button lists) are reached by reflection, and a **contract test suite** asserts those members exist with the
expected types/accessibility against the real assembly.

## Architecture (atomic / one-directional dependency flow)

```
src/Core/        pure C#, no Unity — Model + Ports + CommanderController   (unit-tested)
src/Game/        adapters implementing the ports over Assembly-CSharp/Unity (only layer touching the game)
src/Ui/          atomic uGUI — UiFactory atoms → CommanderPanel/MapOverlay organisms → screens/pages
src/Patches/     Harmony seams (menu badge, map tick driver, MFD CMD button)
src/Composition/ CommanderRuntime — builds the graph, owns the loop
src/Plugin.cs    BepInEx entry / composition root
tests/Core/      xUnit over Core only (no engine), fakes for the ports
tests/GameContract/  Mono.Cecil tests asserting the game-member contract against the real assembly
```

`Plugin → Composition → {Patches, Ui, Game} → Core`. The UI renders from one immutable `CommanderState`;
the controller is pure and tested; only `Game/` references the engine — so a game update breaks `Game/` +
`Patches/`, not the logic or UI.

## Build, test, run

Prereqs: **.NET SDK 8**, the game installed via Steam, and **Git Bash** (Windows). The build needs the
game's DLLs, which are **not** in this repo (they're the game's IP — see [Legal](#legal)).

```bash
# 1. one-time: build the gitignored .sandbox mirror, install BepInEx, copy game DLLs into lib/ (PowerShell)
pwsh ./scripts/setup-sandbox.ps1

# 2. (optional) install the demo mission so it shows in the in-game browser (PowerShell)
pwsh ./scripts/install-demo-mission.ps1

# 3. quality gate — builds against the real assembly + runs all tests (no game launch needed)
./scripts/check.sh

# 4. build the mod (auto-deploys into .sandbox) and launch the modded game (Steam must be running)
./scripts/run.sh
```

Enable the shared git hooks once per clone:

```bash
git config core.hooksPath .githooks
```

## Quality gates

- **pre-commit** (`.githooks/pre-commit`) — fast: Core logic tests.
- **pre-push** (`.githooks/pre-push`) — full `scripts/check.sh` when the game DLLs are present, else Core
  tests. Mirrors CI so "passes locally" means "passes CI".
- **CI** (`.github/workflows/ci.yml`) — runs Core tests on every push/PR; also builds the plugin and runs
  the game-contract tests when the game DLLs are available to the runner.

The game-contract tests (`tests/GameContract`) load the real `Assembly-CSharp.dll` via Mono.Cecil
(metadata only — no Unity execution) and assert every game member the mod calls or reflects on still exists
with the expected type/accessibility. A game update that renames something fails CI here, not in play.

## Try the demo

1. `./scripts/run.sh` → on the main menu you should see "Commander mod loaded".
2. Load the **Commander Debug** mission → join **Boscali** → spawn the trainer.
3. Press **M** → click the **CMD** bezel button → **Place Objective** → click the map.
4. The two Boscali corvettes head to your objective; the panel and map overlay update.

## Pinned environment

| Thing | Value |
|---|---|
| Game build-hash | `2fdbba8b7` (re-run the contract tests after a game update) |
| Mod stack | BepInEx 5 (x64 Mono) + HarmonyX |
| Plugin TFM | `netstandard2.1`; tests `net8.0` |

## Legal

The game's assemblies (`Assembly-CSharp.dll`, `UnityEngine.*`, etc.) are the property of Shockfront Studios
and are **not** distributed here — `lib/`, `.sandbox/`, and `decompiled/` are gitignored. You must own the
game; `scripts/setup-sandbox.ps1` sources those DLLs from your local install. This project is for
interoperability/modding of software you own. MIT-licensed for the mod's own source (see `LICENSE`).
