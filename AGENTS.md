# AGENTS.md

Tool-agnostic entry point for every AI coding agent in this repository (Claude Code, Codex, Cursor, …).
The same content reaches Claude Code through the root `CLAUDE.md` pointer.

## What Nucleus is

A mod platform for **Nuclear Option** (Steam `2168680`, Unity 2022.3 Mono, BepInEx 5 + HarmonyX). One host
plugin owns the shared overlay UI, the per-frame tick, and the game-access services; individual mods register
an `IMod` against it and each gets its own in-game bezel button. The flagship mod, **Commander**, is an
indirect *Majesty*-style command layer — the player drops an objective on the map and the game's own units
execute it — driven by an autonomous theater AI (`CommanderBrain`). The north star is **Nucleus Dynamic
Warfare**: a long-lived campaign where both factions run AI commanders, with whole-campaign save/resume proven
deterministic headlessly.

The game's assemblies are its IP: `lib/`, `.sandbox/`, `decompiled/` are gitignored and never shipped.

## Repository layout

- `libs/` — shared SDK libraries (netstandard2.1, NuGet-published). Two tiers:
  - **Pure, Unity-free, deterministic**: `Nucleus.Domain` (leaf — types live under `Nucleus.Core.*`),
    `Nucleus.Squads`, `Nucleus.Production`, `Nucleus.Campaign`, `Nucleus.Sim`.
  - **Engine/UI-touching**: `Nucleus.GameSdk` (the codegen reflection seam to the game), `Nucleus.Ui` (uGUI
    kit + the `NativeUi` native-widget harvest). `Nucleus.Abstractions` is the `IMod` host contract.
- `apps/` — thin BepInEx plugins: `Nucleus.Platform` (host/loader) + the mods `Commander`, `Build`, `Squad`,
  `Warfare`. Apps are composition only — wiring, not logic.
- `tools/` — `Nucleus.CodeGen` (manifest-driven typed game SDK), `Nucleus.Evolve`, `Nucleus.Installer`,
  `Nucleus.LogAudit`.
- `sdk/` — the `Nucleus.Sdk` metapackage + the `dotnet new nucleus-mod` template.
- `tests/` — `Core` (pure Domain logic), `Nucleus.Architecture.Tests` (the dependency-DAG + purity + design
  validators), `Nucleus.Sim.Tests` (headless campaign-brain e2e — the determinism canary), `Nucleus.LogAudit.Tests`,
  `Nucleus.Installer.Tests` (all headless); `GameContract` + `Nucleus.Integration.Tests` (game-coupled).

## Read order

1. This file.
2. `.agents/rules/` — the enforceable project rules (determinism, layer-discipline, no-legacy-code, testing,
   documentation, design-system, gameplay-invariants). The architecture-test validators encode these; read them
   before changing a lib boundary, the brain, the UI palette, or persistence.
3. The directory `AGENTS.md` for the area you're editing.

## Hard rules (non-negotiable; enforced by tests + the WAE build)

- **Determinism.** The campaign sim and save/resume are byte-identical across runs. Never `string.GetHashCode()`
  or `HashCode.Combine` (per-process seed), never `DateTime.Now/UtcNow`, never `System.Random`/`UnityEngine.Random`
  in the pure libs — use `Fnv1a` / `DeterministicRng`. `Nucleus.Sim.Tests` is the canary; a regression there is a
  hard stop, never a reason to weaken the test.
- **Layer discipline.** References point downward only; the pure libs never reference `UnityEngine`; no app
  references another app; `Campaign` is the only lib above both `Squads` and `Production`. Enforced by
  `Nucleus.Architecture.Tests`.
- **Game access only through the seam.** Reach the game via the codegen'd `GameSdk`/`NativeAssets`/`NativeUi`,
  not ad-hoc reflection or magic strings in apps. New game members go in the `tools/Nucleus.CodeGen` manifest
  (contract-guarded), not as hand-written reflection.
- **Apps stay thin.** Logic lives in libs; apps compose and wire. Enforced by `AppThinnessValidator`.
- **Design system.** UI colors come from `Theme`/`NativeColors`/`ObjectiveVisuals`, sizes from `UiTokens`,
  strings from `UiStrings`/`ObjectiveText` — never hardcoded. `Nucleus.Ui` holds no live mutable campaign model.
  Enforced by `DesignSystemValidator` + `UiStatelessnessValidator` + `SsotValidator`. See
  `.agents/rules/design-system.md` for the color semantics (green = on/selected, red = destructive only).
- **Gameplay invariants.** The default genome reproduces stock behavior; personalities come from the genome,
  not code forks; the war must keep progressing; no phantom objective kinds. See
  `.agents/rules/gameplay-invariants.md`.
- **No legacy/dead code.** No commented-out code, orphan shims, or unreferenced members — unused fails the
  `TreatWarningsAsErrors` build (public/internal surfaced by `scripts/deadcode.ps1`). Delete, don't disable.
- **Comments are self-documenting code.** Explain non-obvious *why*, not narration, history, or planning. No
  phase/review/dev-log tags in comments or docs.

## Build · verify (headless)

- Full build, warnings-as-errors: `dotnet build Nucleus.sln -c Release -p:TreatWarningsAsErrors=true`
  (the `Makefile`/`scripts/check.{ps1,sh}` wrap this; `scripts/audit.ps1` is the full dashboard).
- Headless gate (no game DLLs): `Core` + `Architecture` + `Sim` + `LogAudit` + `Installer`. The pre-commit hook
  runs Core+Architecture+Sim+Installer; pre-push runs the full `check.sh`.
- The plugin compiles against the real, unmodified `Assembly-CSharp.dll`, so the compiler enforces the game's
  actual accessibility; a game update fails the codegen contract test, not the user.

## When in doubt

- Boundaries / determinism / persistence → `.agents/rules/` + `Nucleus.Architecture.Tests`.
- How the brain tasks units → `libs/Nucleus.Campaign/Command/CommanderBrain.cs` (pure) and the `Sim.Tests`.
- Game integration points → `libs/Nucleus.GameSdk` + `tools/Nucleus.CodeGen` manifest.

Never guess on determinism, layer discipline, or the save format. If a change can't pass a canary, redesign it.
