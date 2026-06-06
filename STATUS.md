# STATUS — Nucleus build ledger

> Machine-readable progress ledger. The autonomous loop reads this FIRST every wake to find the next
> action. Gate codes: ① spec ② test ③ review ④ playtest. State: TODO / WIP / GATE-n / DONE / BLOCKED.
> Update this on every state transition. Source of truth for "what's next" — survives context compaction.

**Branch:** `nucleus-platform` · **Baseline (known-good):** build 0 warnings · 118 Core · 11 GameContract (2026-06-06)
**Current phase:** Phase 3 — Host/Platform (spec written; P3a Abstractions DONE)
**Next action (headless, while P3c playtest pending):** deepen the Sim — add invariants for production-within-funds and combined-arms phase advancement (read ProductionNeeds / operation CombatPhase over the run), plus a multi-seed fuzz loop. Then optionally coverage wiring (coverlet) or draft docs/ deliverables. Resume P3d (loader UI) + P3c button/pan migration once playtests/results/P3-host-tick.md lands.
**PENDING PLAYTEST:** playtests/P3-host-tick.md (host-driven tick — confirm panel/commander still work). Check playtests/results/ each wake.
**Sim landed:** 6 headless invariants over the real brain (determinism, no-NaN, 2000-tick stability, objectives, tasks, war-progresses). Gate = 6 layers.
**Gate now:** 5 layers — build 0w · unit-core 118 · arch 9 · contract 11 · integration 8 (host lifecycle headless-proven).
**P3b core done (not live):** src/Host/{LogSink, GameServices}; ModRegistry now in Nucleus.Abstractions (tested). CommanderRuntime still drives live.
**Gate now:** `pwsh scripts/audit.ps1` → AUDIT: PASS (build 0w · unit-core 118 · arch 9 · contract 11). 7 libs: Domain/Squads/Production/Campaign/GameSdk/Ui (+Abstractions next).
**src shell now:** Plugin.cs, Composition/CommanderRuntime, Patches/{MainMenuBadge,DynamicMapTick,VirtualMFD,AircraftTasking}, Game/CommanderService, Ui/{CommanderPanel,CommanderMapScreen,MapOverlay,OrderColors}.

## Phase status
| Phase | Title | State | Notes |
|-------|-------|-------|-------|
| 0 | Tooling & ledger foundation | DONE | sln+props+build helpers, arch test (9, synthetic-proven), gate scripts, active hooks, warnings-clean, CI headless gate. TestKit/Sim/coverage/api-snap sequenced into P1; LogAudit→pre-P3 |
| 1 | Extract pure libs (Domain/Squads/Production/Campaign) | TODO | namespaces frozen at CommanderLayer.* |
| 2 | Extract GameSdk + Ui + retarget codegen | TODO | |
| 3 | Stand up host; Commander first | TODO | riskiest — Canvas/tick ownership inversion |
| 4 | Split Build | TODO | one host queue, no double-buy |
| 5 | Split Squad | TODO | external SquadRoster ctor |
| 6 | Warfare + SDK packaging + dual-faction + persistence | TODO | north-star |
| 7 | Rename pass (CommanderLayer.*→Nucleus.*, repo+folder no_nucleus) | TODO | human folder-rename touchpoint |

## Work-items in flight
| ID | Phase | Item | Gate | Owner | Last gate result | Next action |
|----|-------|------|------|-------|------------------|-------------|
| P3c | 3 | live host flip (tick) | ④ playtest | loop | built, playtest queued | await playtests/results/P3-host-tick.md |
| P6-sim | 6 | campaign Sim harness | — | loop | core done (6 invariants) | deepen: production/phases/fuzz |

## Pending playtests (Unity-gated, awaiting human)
- **P3-host-tick** (playtests/P3-host-tick.md) — confirm the host-driven tick still drives the Commander panel
  + AUTO commander + manual orders, no exceptions. Return → playtests/results/P3-host-tick.md.

## Gates / commands
- Fast: `pwsh scripts/check.ps1` (build + changed-project unit + arch)
- Full: `pwsh scripts/audit.ps1` (everything → PASS/FAIL dashboard + artifacts/audit-summary.json)
- Baseline today (pre-monorepo): `dotnet build src/CommanderLayer.csproj -c Release` + `dotnet test tests/Core` + `dotnet test tests/GameContract`
