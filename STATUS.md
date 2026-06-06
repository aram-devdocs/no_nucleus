# STATUS — Nucleus build ledger

> Machine-readable progress ledger. The autonomous loop reads this FIRST every wake to find the next
> action. Gate codes: ① spec ② test ③ review ④ playtest. State: TODO / WIP / GATE-n / DONE / BLOCKED.
> Update this on every state transition. Source of truth for "what's next" — survives context compaction.

**Branch:** `nucleus-platform` · **Baseline (known-good):** build 0 warnings · 118 Core · 11 GameContract (2026-06-06)
**Current phase:** Phase 3 — Host/Platform (spec written; P3a Abstractions DONE)
**Next action:** P3b-probe — create tests/Nucleus.Integration.Tests (net8.0) referencing Nucleus.Abstractions + a FakeMod/FakeGame, run a ModRegistry-style lifecycle test, to learn whether the Unity-referencing contract loads headlessly. If YES → move ModRegistry to a referenceable spot + write real host-lifecycle integration tests (FakeGame: register→init→tick→enable/disable, shared queue, no double-buy). If NO → host stays build+playtest-gated; proceed to the live flip (ModContext/IModUi/ModHost + reroute Plugin/patches) and queue a playtest packet.
**P3b core done (not live):** src/Host/{LogSink, GameServices, ModRegistry}. CommanderRuntime still drives live (behavior unchanged).
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
| P3b-probe | 3 | headless host-testability probe | — | loop | next | net8.0 test loads Abstractions? decides verify strategy |

## Pending playtests (Unity-gated, awaiting human)
_(none yet)_

## Gates / commands
- Fast: `pwsh scripts/check.ps1` (build + changed-project unit + arch)
- Full: `pwsh scripts/audit.ps1` (everything → PASS/FAIL dashboard + artifacts/audit-summary.json)
- Baseline today (pre-monorepo): `dotnet build src/CommanderLayer.csproj -c Release` + `dotnet test tests/Core` + `dotnet test tests/GameContract`
