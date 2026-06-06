# STATUS — Nucleus build ledger

> Machine-readable progress ledger. The autonomous loop reads this FIRST every wake to find the next
> action. Gate codes: ① spec ② test ③ review ④ playtest. State: TODO / WIP / GATE-n / DONE / BLOCKED.
> Update this on every state transition. Source of truth for "what's next" — survives context compaction.

**Branch:** `nucleus-platform` · **Baseline (known-good):** build 0 warnings · 118 Core · 11 GameContract (2026-06-06)
**Current phase:** Phase 3 — host flip PLAYTEST PASSED (P3-host-tick: plugin loaded, 4/4 patches, host-driven tick reached runtime, 0 exceptions). Unblocks P3d/P4/P5.
**Next action:** ONE playtest now verifies a big batch — encourage the user to run `scripts/run.ps1` once; `audit.ps1 -LogPath` will confirm 4 self-tests in one shot (loader-ui-built, build-mod-loaded, squad-mod-loaded, bezel-buttons-attached). Meanwhile, smaller hardening: persist the loader toggle + bezel-button state to BepInEx config (P3d follow-up). The big remaining feature is the host real UI layer (host-owned Canvas → mods get real panels: Build buy-menu, Squad manager) — that's the Canvas-ownership inversion, do it after the stack is playtest-verified to avoid compounding unverified UI.
**Verification debt:** P3-host-tick PASSED; loader/build/squad/buttons built + self-instrumented, awaiting one run. (tag → dotnet pack+push NuGet, gated on api-snapshot) + `setup-sdk` script (populate consumer lib/ from their Steam install) + fix the metapackage `dotnet pack` no-op. Then dual-faction Sim (both sides run brains) toward the north-star. Resume P3d (loader UI) once playtests/results/P3-host-tick.md lands → then `audit.ps1 -LogPath <log>` audits it mechanically.
**Gate now 7 layers:** build 0w · unit-core 118 · arch 9 · sim **17** · logaudit 5 · contract 11 · integration 8.
**Headless runway note:** most remaining work (P3d loader UI, P4 Build mod, P5 Squad mod) is **playtest-gated** on P3-host-tick. Remaining headless north-star item: **campaign persistence** (save/resume model + round-trip tests) — do that next; then park on the playtest if nothing else is headless-verifiable.
**Docs landed:** docs/TESTING.md, docs/TESTING-WORKSHEET.md, docs/DEPLOYMENT.md.
**SDK DX so far:** 7 libs packable + IP-clean; `dotnet new nucleus-mod` template smoke-tested; `tools/Nucleus.LogAudit` CLI ready.
**PENDING PLAYTEST:** playtests/P3-host-tick.md (host-driven tick — confirm panel/commander still work). Check playtests/results/ each wake.
**Sim landed:** 14 headless tests over the real brain (determinism, no-NaN, 2000-tick stability, objectives, tasks, war-progresses, operations-opened, phases-advance, 6-seed fuzz). Gate = 6 layers.
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
| P3d | 3 | mod loader UI (MODS menu) | ④ | loop | built, playtest queued | await playtests/results/P3d-loader.md |
| P4 | 4 | Build as its own plugin | ④ | loop | built (no-skew verified) | playtest auto-verifies build-mod-loaded |
| P5 | 5 | Squad as its own plugin | ④ | loop | built (no-skew verified) | playtest auto-verifies squad-mod-loaded |
| P-buttons | 3-4 | host bezel-button registry | ④ | loop | built (CMD untouched) | next run auto-verifies bezel-buttons-attached |
| P-persist | 3 | persist loader/button enable-state | — | loop | next (small) | bind ModRegistry enabled to BepInEx config |
| P6-sdk | 6 | SDK NuGet packaging | — | loop | libs packable + template done | setup-sdk + release.yml + metapackage-pack fix |

## Pending playtests (Unity-gated, awaiting human)
- ✅ **P3-host-tick — PASSED** (playtests/results/P3-host-tick.md). Host flip confirmed in-game.
- Next playtest will be richer: host now emits [NUCLEUS:SELFTEST]/[NUCLEUS:METRIC] lines → `audit.ps1 -LogPath` auto-verifies.

## Gates / commands
- Fast: `pwsh scripts/check.ps1` (build + changed-project unit + arch)
- Full: `pwsh scripts/audit.ps1` (everything → PASS/FAIL dashboard + artifacts/audit-summary.json)
- Baseline today (pre-monorepo): `dotnet build src/CommanderLayer.csproj -c Release` + `dotnet test tests/Core` + `dotnet test tests/GameContract`
