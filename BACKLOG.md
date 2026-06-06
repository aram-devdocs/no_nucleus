# BACKLOG — Nucleus

> Ordered queue of work-items. Each is small (≤ ~6 commits), spec-first (no code before an approved
> `specs/<phase>/<item>.md`), and tagged **HV** (headless-verifiable — unit/integration/sim/arch) or
> **PT** (needs a Unity playtest). The loop pulls the top HV item when nothing is mid-pipeline, so it
> never idles waiting on a playtest. Discoveries during work are appended here, never folded into a diff.

## Phase 0 — Tooling & ledger (HV unless noted)
- [x] P0-ledger — STATUS/BACKLOG/DECISIONS/north-star files **HV**
- [x] P0-props — root `Directory.Build.props` (conservative; monolith verified still 0-warnings, 118+11) **HV**
- [x] P0-sln — `Nucleus.sln` (4 existing projects) + `build/GameReferences.props`, `Deploy.targets`, `Packaging.props` (inert until imported) **HV**
- [ ] P0-hooks — commit `.githooks/` (pre-commit/pre-push) + `core.hooksPath`; `scripts/check.ps1` + `scripts/audit.ps1` **HV**
- [ ] P0-arch — `tests/Nucleus.Architecture.Tests` (Cecil dependency-graph/DAG/Unity-free rules; passes on current single DLL) **HV**
- [ ] P0-testkit — `tests/Nucleus.TestKit` (FakeGame) + `tests/Nucleus.Integration.Tests` scaffold (≥1 real assertion) **HV**
- [ ] P0-sim — `tests/Nucleus.Sim` harness + `tests/Nucleus.Sim.Tests` (seeded PRNG, 1 invariant over the existing brain) **HV**
- [ ] P0-coverage — coverlet wiring + ReportGenerator + per-lib threshold gate in audit.ps1 **HV**
- [ ] P0-apisnap — public-API snapshot scaffolding (no shipped libs yet; wires the gate) **HV**
- [ ] P0-logaudit — `tools/Nucleus.LogAudit` CLI (parse BepInEx log → JSON verdict, non-zero on FAIL) **HV**
- [ ] P0-ci — CI split: `ci.yml` (pure always-on), `release.yml` (tag), `nightly.yml` (sim) **HV**

## Phase 1 — Extract pure libs (each extraction guarded by arch + per-lib unit + coverage)
- [ ] P1-domain — create `libs/Nucleus.Domain`, move Core/Model + shared Command primitives; repoint tests/Core **HV**
- [ ] P1-squads — `libs/Nucleus.Squads` (Squad/SquadRoster/SquadFormer) + `Nucleus.Squads.Tests` **HV**
- [ ] P1-production — `libs/Nucleus.Production` (ProductionQueue/Planner/Catalog) + tests **HV**
- [ ] P1-campaign — `libs/Nucleus.Campaign` (brain/operations/objectives/planning/HqView) + tests **HV**

## Phase 2–7 — see plan (specs to be drafted as each phase is pulled)
- [ ] P2 — extract GameSdk + Ui + retarget codegen output paths **HV (+PT smoke)**
- [ ] P3 — host/Platform + IMod + Commander as first mod **PT**
- [ ] P4 — split Build **PT**
- [ ] P5 — split Squad **PT**
- [ ] P6 — Warfare + SDK pack + dotnet template + dual-faction sim + persistence **HV (+PT)**
- [ ] P7 — rename CommanderLayer.*→Nucleus.*, `gh repo rename no_nucleus`, folder rename (human), doc rewrite **PT/human**

## Discovered (triage later)
- **codegen nullable warnings** — `tools/CommanderLayer.CodeGen/Program.cs` emits 7 CS86xx warnings
  (Nullable=enable inline). Invisible to the old baseline (built `src/` only). Must be fixed (or
  Nullable scoped) before the warnings-as-errors gate (P0-hooks) goes solution-wide. **HV**
