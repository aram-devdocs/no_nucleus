# DECISIONS — Nucleus

> Decision log + per-phase retros. Durable lessons also go to the agent's persistent memory.
> Append-only; newest at top of each section.

## Architecture decisions
- **2026-06-06 · Phase 1 lib boundaries (from Core dependency analysis).** Domain leaf = 20 closure-verified
  pure files (Model/*, Generated/*, Command/{AutonomyLevel,Doctrine,BattleLog,Objective,WorldSnapshot},
  Ports/*, Planning/ThreatAssessor, Roles/RoleClassifier). **Entanglement found:** `RoleFamily`+`Composition`
  reference `CombatPhase` (currently inside `PhaseGates.cs`), and Production types (ConvoyCatalog/
  ProductionPlanner/ProductionQueue) use `Composition`→`RoleFamily`. To keep Squads and Production
  *independent* (plan rule), `RoleFamily`, `Composition`, and a split-out `CombatPhase`(+`ForceState`) move
  into **Domain** during the Squads/Production sub-step — NOT in P1-domain (kept minimal/low-risk). Also
  noted (defer): `CommanderBrain` is a god-file (decompose later, not now — behavior-preserving move first).
- **2026-06-06 · Shared campaign-domain lib, not cross-plugin calls.** AUTO brain stays a pure
  `CommanderBrain.Tick(snapshot, state)` calling down into `Nucleus.Squads`/`Nucleus.Production`;
  never a runtime call into the Squad/Build plugins (BepInEx load-order/absence fragility). Human and
  brain operate the same domain types + the same host-owned live state.
- **2026-06-06 · Single host owns Canvas/tick/contended-patches.** `Nucleus.Platform` is the only plugin
  patching `DynamicMap.Update` / `VirtualMFD.VirtualMFD_onMapMaximized` / `MainMenu.Start`; mods register
  via `IMod` + `[BepInDependency(HardDependency)]` (explicit registration, not reflection scan).
- **2026-06-06 · Namespaces frozen at `CommanderLayer.*` until Phase 7.** Folder/assembly ≠ namespace in C#;
  do the structural split first, rename mechanically last — keeps every step low-churn and test-green.
- **2026-06-06 · Brand = Nucleus; repo+folder = `no_nucleus`; distribution = NuGet (SDK) + Thunderstore +
  native loader + source + Nexus + Steam Workshop mission "Nucleus Dynamic Warfare".** (User decisions.)

## Scope decisions
- **2026-06-06 · Host lifecycle IS headless-testable (probe result).** A net8.0 test CAN load the
  Unity-referencing `Nucleus.Abstractions` (+Ui) and construct/drive its types, as long as it makes no actual
  Unity runtime calls and the game/Unity DLLs are copied next to it (`<None Include="$(Lib)\*.dll">`).
  Therefore `ModRegistry` moved into Nucleus.Abstractions and `tests/Nucleus.Integration.Tests` drives the
  real registry with a `FakeMod` (8 tests). These are **game-coupled** (need lib/) so they run in the
  contract gate, not cloud-CI. The Unity-touching host parts (Canvas, IModUi, button attach) remain
  playtest-gated; their orchestration (registry lifecycle) is now headless-proven.
- **2026-06-06 · Logging seam (NucleusLog) + InternalsVisibleTo for the lib split.** Shared libs must not
  reference the BepInEx `Plugin` static. Added `CommanderLayer.Core.NucleusLog` (pure Action<string> sinks in
  Domain; host wires them in Plugin.Awake). `CommanderDebugProbe` (reads `Plugin.CommanderDebug`) stays in the
  app, not GameSdk. GameSdk uses `InternalsVisibleTo("CommanderLayer")` to preserve the exact same-assembly
  accessibility the mechanical move relied on — the deliberate **SDK public surface** is defined later (Phase 6
  packaging), not widened ad hoc now.
- **2026-06-06 · Per-lib test projects deferred; tests/Core stays the aggregate for Phase 1.** The 118
  existing tests in tests/Core already exercise squad/production/campaign logic and reference each extracted
  lib. Splitting them into Nucleus.Squads.Tests/.Production.Tests/.Campaign.Tests is churn without new
  coverage right now; do it as a later reorg (or when adding NEW per-lib tests). Arch test + 118 + contract
  guard each extraction. ThreatBoard left in src/Core (→Campaign lib later) — not needed by Squads/Production.

## Scope decisions (cont.)
- **2026-06-06 · Dual-faction Sim asserts PLANNING, not attrition.** `DualSimWorld` runs `CommanderBrain` for
  both sides over one battlefield. Robust headless invariants: both brains detect the enemy + generate
  objectives (both factions command), deterministic, no-NaN, no-throw. **Not** asserted: that ground units
  assault/attrit — combined-arms doctrine gates ground advance behind softening the target (Strike phase),
  which needs air/artillery, and aircraft are steered by intent zones (not per-unit `UnitTask`), which the
  coarse sim doesn't model. Forcing a "war resolves" assertion would misrepresent the doctrine. Modeling
  aircraft-intent + artillery softening in-sim is a future deepening if we want attrition-level invariants.

## Pending decisions (options + recommended default; escalate before the gated action)
- **Repo + folder rename** (`commander` → `no_nucleus`): irreversible-ish outward action. Default: do
  `gh repo rename no_nucleus` at Phase 7, hand the human the local folder-rename steps. **Park for explicit go.**
- **Publishing** NuGet / Thunderstore / Steam Workshop: requires accounts + secrets. Default: prepare
  packages + `docs/DEPLOYMENT.md`, park for the human to create accounts/secrets and approve first publish.

## Per-phase retros
- **2026-06-06 · Phase 0 (Tooling & ledger) retro.** Landed: durable ledger (STATUS/BACKLOG/DECISIONS/
  north-star); `Nucleus.sln` + conservative `Directory.Build.props` + `build/` helpers over the existing
  4 projects with the monolith still 0-warnings/118/11; `Nucleus.Architecture.Tests` (Cecil DAG/ownership
  rules) **with synthetic proofs the rules bite** — the key call that avoids a false-green vacuous pass;
  `scripts/check.ps1`+`audit.ps1` (PASS/FAIL dashboard + JSON), `check.sh` to the solution gate; `.githooks`
  modernized + activated via `core.hooksPath` (verified: pre-commit ran on commit); codegen made
  warnings-clean (inherit Nullable=disable) so `-p:TreatWarningsAsErrors=true` is meaningful; `ci.yml` to a
  headless-always-on + game-coupled-conditional gate. **Lesson:** a root `Directory.Build.props` only risks
  properties the existing projects don't set themselves (Nullable/warnings) — keep it conservative and apply
  warnings-as-errors at gate time (CLI), not globally, to protect the inner loop. **Lesson:** building the
  full `Nucleus.sln` can't run on cloud CI (monolith needs game DLLs); run the Unity-free projects directly.
  **Re-sequenced:** TestKit/Sim/Integration/coverage/api-snapshot build against the *extracted* libs, so they
  moved into Phase 1 rather than being stood up against the monolith.
