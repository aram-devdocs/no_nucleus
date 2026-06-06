# BACKLOG — Nucleus

> Ordered queue of work-items. Each is small (≤ ~6 commits), spec-first (no code before an approved
> `specs/<phase>/<item>.md`), and tagged **HV** (headless-verifiable — unit/integration/sim/arch) or
> **PT** (needs a Unity playtest). The loop pulls the top HV item when nothing is mid-pipeline, so it
> never idles waiting on a playtest. Discoveries during work are appended here, never folded into a diff.

## Phase 0 — Tooling & ledger (HV unless noted)
- [x] P0-ledger — STATUS/BACKLOG/DECISIONS/north-star files **HV**
- [x] P0-props — root `Directory.Build.props` (conservative; monolith verified still 0-warnings, 118+11) **HV**
- [x] P0-sln — `Nucleus.sln` (4 existing projects) + `build/GameReferences.props`, `Deploy.targets`, `Packaging.props` (inert until imported) **HV**
- [x] P0-hooks — `.githooks/` modernized (Core+arch / full check.sh) + `core.hooksPath` activated; `scripts/check.ps1` + `scripts/audit.ps1` (PASS/FAIL dashboard + JSON) **HV**
- [x] P0-arch — `tests/Nucleus.Architecture.Tests` (Cecil DAG/Unity-free/ownership rules + 4 synthetic proofs the rules bite) **HV**
- [x] P0-ci — `ci.yml` modernized: always-on headless gate (Core + arch) on ubuntu + full `check.sh` when lib present. (`release.yml`/`nightly.yml` deferred — need pack targets [P6] and the Sim suite first.)
- [~] P0-testkit/sim/coverage/apisnap — **sequenced into Phase 1**: TestKit(FakeGame)/Integration/Sim/coverage/api-snapshot are built against the real extracted libs (they reference Nucleus.Domain/Campaign), so they land right after P1-domain rather than against the monolith. Tracked under Phase 1.
- [ ] P0-logaudit — `tools/Nucleus.LogAudit` CLI (parse BepInEx log → JSON verdict). **Deferred to pre-Phase-3** (first playtest); audit.ps1 already has the `-LogPath` hook stubbed. **HV**

## Phase 1 — Extract pure libs (each extraction guarded by arch + per-lib unit + coverage)
- [x] P1-domain — `libs/Nucleus.Domain` (20 closure-verified pure files); tests/Core repointed (ProjectRef + glob); codegen coreGenDir → libs/Nucleus.Domain/Generated; contract test reads mirror from Domain.dll; deploy bundles Nucleus.*.dll. Gate PASS (0w/118/9/11), deploy verified. **HV**
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
- [x] **codegen nullable warnings** — resolved: codegen inherits `Nullable=disable` from root props
  (build tool, not SDK surface). Solution now 0 warnings under `-p:TreatWarningsAsErrors=true`.
