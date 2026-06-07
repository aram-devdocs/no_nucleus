# OVERNIGHT — autonomous overhaul loop ledger

> Self-directed run started Sat 2026-06-06 ~21:40 MDT, target Sun 2026-06-07 10:00 MDT.
> Director: Claude (Opus 4.8). Plan: `~/.claude/plans/im-playing-the-mod-distributed-island.md`.
> Integration branch: `auto/overnight` (master stays pristine; one consolidated PR opened at the end).
> The loop reads THIS file first each wake to find the next action. Every PR gated on full `audit.ps1` PASS.

## Guardrails (non-negotiable)
- Merge only to `auto/overnight`, never master; one human PR at the end.
- Gate every merge on local `scripts/audit.ps1` → `AUDIT: PASS` (cloud CI can't compile plugins).
- Never weaken/skip/delete existing tests or arch rules. New tests only. Determinism fingerprint = canary.
- No `--admin`, no `gh release`, no `git tag v*`, no publish, no repo rename.
- Every visual/UX change must be backed by a screenshot actually viewed (scripts/visual-probe.ps1).

## Progress
| WS | Title | State | Verify | PR |
|----|-------|-------|--------|----|
| WS0 | In-game visual harness (screenshots) | DONE | live: 6/6 shots, CMD crop legible | #1 |
| WS1 | AI objective mix (capture/destroy/defend) | DONE | audit PASS (sim 33) + live CMD shows 2×Capture+1×Destroy | #2 |
| WS3 | AI narration / barks in feed | DONE | audit PASS (sim 36) + live CMD FEED shows narrated ops | #3 |
| WS6 | Map selection legibility | DONE | audit PASS + live: intent header + status-colored squad lines | #4 |
| WS5 | In-flight HUD + world markers | DONE | audit PASS + live: bottom-right HUD shows ops+intent while flying | #5 |
| WS7 | Squad legibility & usability | DONE | audit PASS (sim 39) + live: SQD shows "4× MBT, 1× IFV"; assign deferred | #6 |
| WS8 | Build clarity | DONE | audit PASS (core 131) + live: aircraft note + Funds/Queued/After | #7 |
| WS2 | Personality genomes | DONE | audit PASS + 6 genome tests + determinism canary; enemy-AI-driving caveat | #8 |
| WS9 | Theme tokens + APP-6 symbology | DONE | audit PASS + live: clearer markers + HUD contrast | #9 |
| WS11 | Sim-as-lib + Evolve self-play | DONE | audit PASS (sim 41); determinism canary held; honest flat-fitness caveat | #10 |
| WS12 | Squad assign-to-objective UX + map header edge-clamp | DONE | audit PASS + live: ASSIGN list + header clamp | #11 |
| WS4 | Presentation VM layer | NOT DONE | deferred — low user value, higher risk; left for human | — |

## RUN COMPLETE (Sun ~00:25 MDT)
11 workstreams shipped to `auto/overnight` (PRs #1–#11), master untouched. Every explicit user
complaint addressed + harness + evolution infra + usability polish. Director call: STOP new work
before the deadline rather than gamble the clean green branch on the low-value/higher-risk WS4 refactor.
Final consolidated PR `master ← auto/overnight` opened for human review (UNMERGED). Loop ended; no more wakeups.

### Flagged follow-ups for the human
- War may advance only while the map is open (host ticks off DynamicMap.Update) — see WS5 note. Possible "feels janky" cause.
- Enemy-AI driver (DriveEnemyAi) didn't execute its body in the autoloaded+joined harness — confirm enemy actually runs our brain (WS2).
- Evolve fitness is flat on the symmetric scenario — needs asymmetric scenarios / richer fitness before trusting evolved genomes (WS11).
- Native-widget UI restyle (beyond safe theming) — bigger, human-reviewed (WS9).
- WS4 Presentation VM layer — not done.

## Tooling proven this run
- `scripts/visual-probe.ps1 [-NoBuild] [-Tag x]` → launches game, joins Boscali, drives map+panels,
  captures PNGs to `artifacts/screenshots/<tag>/` + legible `-panel`/`-map` crops. Read the crops to SEE the UI.
  Driven by `MissionManager.Update` (NOT DynamicMap.Update — only fires while map maximised; and the game
  pumps no Update on our own MonoBehaviours). Self-flushing `nucleus-shots/probe-trace.log` survives kill.
- Game forces 5120×1440; resolution CLI args are ignored — rely on the zoomed crops for legibility.
- Factions in "Nucleus Dynamic Warfare": Boscali (default join) vs Primeva.

## Known follow-ups discovered
- Recon objectives need roster-aware/personality gating (recon-on-accuracy alone starves scout-less forces) — fold into WS2.
- In-flight view (joined via script) shows NO HUD at all — confirms the "nothing while flying" complaint; WS5 target.
