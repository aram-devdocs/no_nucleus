# Commander — build progress

Single source of truth for phase status + decisions. Plan: `~/.claude/plans/composed-fluttering-crescent.md`.
Gates per phase: ① Spec approved · ② Quality green · ③ Review clean · ④ Accepted.

## Phase status
| Phase | State | Notes |
|---|---|---|
| S0 — de-risk spikes | Built · awaiting playtest | probes committed (879736b); set `CommanderDebug=true`, run sandbox, paste [S0:*] log |
| P0 — foundation fixes | ①②③ ✔ · awaiting ④ playtest | review found NO blocking; S1/S2/N1 fixed (f49d1ef). 42 Core + 11 contract green. ④ = overlay-visibility playtest |

**↳ NEXT PLAYTEST (one run unblocks P0 acceptance + P0.5):** enable `Commander/CommanderDebug` (F1 config),
load Commander Debug, open map, arm an order, hover, place two overlapping orders; paste the BepInEx log
(esp. `[S0:*]` lines) + a screenshot. Meanwhile the loop builds P1/P2 pure-Core (no playtest needed).
| P0.5 — sandbox + terrain | Backlog | depends on S0 terrain probe |
| P1 — squads + operations + brain | ①②③ ✔ · awaiting ④ playtest | brain wired flag-gated (EnableAutoCommander off). Review B1/B2/S1/S2 fixed (5bfefd7): ops complete+free squads, objectives pruned, diff tasking, auto excludes manual-committed. 54 Core. Deferred: S3 focus-fire TargetId (P4), S4 executor O(n²). ④ = enable flag + observe autonomous slice |
| P2 — combined-arms sequencing | ✅ gates + integration | PhaseGates (924fe32) + operations advance CombatPhase cursor, task per-phase — armor holds while artillery/aircraft soften (07d4fbc). 60 Core. Pure+tested; activation playtest-gated with P1 |
| P3 — economy/production | Backlog | depends on S0 convoy spike |
| P4 — intel board + reports | Backlog | |
| P5 — autonomy UI + HUD | Backlog | |
| P6 — native UI component library | Backlog (NEW) | **Codegen** the UI seam: generate typed wrappers for the game's OWN UI components (NuclearOption.UI toggles/border, Button, ObjectiveInfoList rows, MapToolTip, MFDScreen) + ALL visual assets (GameAssets colors/fonts/sprites/icons) into the SDK — 1:1, regenerates on game updates. Re-base UiFactory/Theme/OrderColors to READ from generated accessors (DRY, single source of truth, no hardcoded/skewed values; mod-owned values clearly marked). Runtime clone with graceful fallback. Mostly playtest-gated. |

Spec: `specs/phase-S0-P0.md`.

## S0 findings (filled after the playtest)
| Unknown | Result | Detail |
|---|---|---|
| Kill detection (trackingDatabase prune) | TBD | |
| Unit-id stability (persistentID) | TBD | |
| Convoy contents & arrival | TBD | |
| Terrain water/land probe | TBD | |
| Aircraft intent convergence | TBD | |

## Resolved open decisions
_(recorded as each phase's spec gate closes)_
- P0 force-sizing: **start simple** — Attack/Defend target = clamp(ceil(knownThreatCount × ForceRatio), MinForce, MaxUnitsPerOrder); ForceRatio 1.5; MinForce 1. Move/Capture/Resupply/Build: Max-cap only (threat-independent). Tune later.
- P0 committed-units set: **derived each tick** from Active orders' AssignedUnitIds ∩ live roster (not hand-maintained) — auto-releases on death/Failed/Complete/Clear. Keyed on persistentID; revisit if S0 UID probe shows reuse.
- P0 inner ring = real `ThreatRadius` (3000m), outer = pull radius; independent pixel clamps.
- P0 occlusion: panel docked on mod canvas (verify it still raycasts); order markers de-occluded on `iconLayer` via sibling/alpha, not canvas sort.
- P1 default autonomy: all-**Auto** on load ("do nothing = game runs").
