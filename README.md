# Nucleus

A **mod platform** for **[Nuclear Option](https://store.steampowered.com/app/2168680/Nuclear_Option/)**
(Steam App `2168680`, Shockfront Studios) — Unity 2022.3 (Mono), BepInEx 5 + HarmonyX.

Nucleus turns one mod into a platform: a **host/loader** owns the shared overlay UI, the per-frame tick, and
the game-access services; individual mods register against it and each gets its own in-game button. The first
mod is **Commander** — an indirect, *Majesty*-style command layer (drop an objective on the map, the game's
own units execute) with an autonomous theater AI. The north star is **Nucleus Dynamic Warfare**: a long-lived
campaign where both factions run AI commanders that build forces and fight.

> Unofficial community mod (BepInEx + HarmonyX), not affiliated with or endorsed by Shockfront Studios.
> The game's assemblies are its IP — `lib/`, `.sandbox/`, `decompiled/` are gitignored and never shipped.

## Layout

```
libs/        shared SDK libraries (netstandard2.1, published to NuGet)
  Nucleus.Domain · Nucleus.Squads · Nucleus.Production · Nucleus.Campaign   (pure, Unity-free)
  Nucleus.GameSdk · Nucleus.Ui                                             (engine/UI access)
  Nucleus.Abstractions                                                     (the IMod host contract)
apps/        thin BepInEx plugins (the host + the mods)            tools/   codegen · log-audit · scripts
sdk/         Nucleus.Sdk metapackage + `dotnet new nucleus-mod` template
tests/       Domain · Architecture · Sim · LogAudit (headless) · GameContract · Integration (game-coupled)
```

Dependency rule (enforced by `Nucleus.Architecture.Tests`): references point downward only; pure libs are
Unity-free; no app references another app; `Campaign` is the only lib over both `Squads` and `Production`.

## Build · test · run

A `Makefile` wraps the common workflows (run `make` with no target for the full list):

```sh
make sandbox      # one-time: locate the game, mirror it to .sandbox, copy lib/ DLLs, install BepInEx
make dev          # build all mods -> deploy to .sandbox -> launch the game
make commander    # rebuild+deploy just one mod -> launch (also: platform | build-mod | squad | warfare)
make test         # full gate (8 layers) -> PASS/FAIL dashboard + artifacts/audit-summary.json
make check        # fast gate: build (warnings-as-errors) + Core unit + architecture rules
make logaudit     # turn the last in-game BepInEx log into a mechanical verdict
make mission      # install the demo mission into your user Missions folder
make codegen      # regenerate the typed game SDK (after a game update)
```

The underlying scripts in `scripts/` (`.ps1` / `.sh`) still work directly if you prefer.

The quality gate (`make test` / `scripts/audit.ps1`): **build** (0 warnings) · **unit** (pure Core) ·
**arch** (dependency graph) · **sim** (headless campaign-brain e2e) · **logaudit** · **installer** ·
**contract** (Mono.Cecil vs the real `Assembly-CSharp`) · **integration** (host mod-lifecycle). The pure
layers also run in cloud CI; the game-coupled layers run only where `lib/Assembly-CSharp.dll` is present.
See [docs/TESTING.md](docs/TESTING.md).

## How it integrates with the game

- Reads the local faction (`GameManager.GetLocalHQ/GetLocalFaction`), units (`UnitRegistry.allUnits`), and
  fog-of-war threats (`FactionHQ.trackingDatabase`); classifies units by role from their `UnitDefinition`.
- Commands ground units per-unit via `UnitCommand.SetDestination`/`SetHoldPosition` (no faction-objective
  "stampede"); steers idle **aircraft** (not `ICommandable`) via an `AIPilotCombatModes.NoTarget` postfix.
- The host drives everything from a single Harmony postfix on `DynamicMap.Update` and attaches mod buttons
  from `VirtualMFD.VirtualMFD_onMapMaximized`. Borrows the game's own font/HUD colors/button sprites.

### Type-safety — a generated SDK

The plugin **compiles against the real, unmodified `Assembly-CSharp.dll`**, so the compiler enforces the
game's actual accessibility. A single declarative manifest in `tools/*.CodeGen` lists every game member used;
the generator verifies them against the real assembly and emits typed reflection accessors + enum mirrors +
a drift contract test. No magic strings; a game update fails the contract test, not the user.

## For mod developers

`dotnet new nucleus-mod -n MyMod` scaffolds a starter mod (a thin `[BepInPlugin]` that registers an `IMod`
with the platform). Reference the `Nucleus.Sdk` NuGet metapackage; run `scripts/setup-sdk.ps1 -GamePath <…>`
to populate `lib/` with your own game DLLs (never shipped). See [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md).

## Contributing with AI agents

[`AGENTS.md`](AGENTS.md) is the source of truth for AI coding agents; the per-directory `AGENTS.md` files and
[`.agents/rules/`](.agents/rules) carry the enforceable rules (determinism, layer discipline, no-legacy-code,
testing, documentation). `CLAUDE.md` is a symlink to `AGENTS.md`.
