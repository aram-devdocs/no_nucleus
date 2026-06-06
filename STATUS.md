# STATUS — Nucleus build ledger

> Machine-readable progress ledger. The autonomous loop reads this FIRST every wake to find the next
> action. Gate codes: ① spec ② test ③ review ④ playtest. State: TODO / WIP / GATE-n / DONE / BLOCKED.
> Update this on every state transition. Source of truth for "what's next" — survives context compaction.

**Branch:** `nucleus-platform` · **Baseline (known-good):** build 0 warnings · 118 Core · 11 GameContract (2026-06-06)
**Current phase:** Phase 3 — Host/Platform + IMod + Commander as first mod (PHASE 2 COMPLETE: 7 libs extracted)
**Next action:** WRITE SPEC FIRST (specs/phase-3/) — this is the riskiest step (Canvas/tick/patch ownership inversion). Plan: create libs/Nucleus.Abstractions (IMod/IModContext/IModUi/IGameServices/IButtonRegistry/ModPlatform). Then split Plugin.cs+CommanderRuntime into a host (owns single Canvas/tick pump/the 3 contended patches/native capture/button registry/shared services) + Commander as the first IMod (CommanderService + the 4 commander panels + AircraftTaskingPatch). Behavior identical; verify with integration tests + (later) playtest.
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
| P3-spec | 3 | host/Platform spec | ① | loop | next | write specs/phase-3 before any host code |

## Pending playtests (Unity-gated, awaiting human)
_(none yet)_

## Gates / commands
- Fast: `pwsh scripts/check.ps1` (build + changed-project unit + arch)
- Full: `pwsh scripts/audit.ps1` (everything → PASS/FAIL dashboard + artifacts/audit-summary.json)
- Baseline today (pre-monorepo): `dotnet build src/CommanderLayer.csproj -c Release` + `dotnet test tests/Core` + `dotnet test tests/GameContract`
