# Commander — build progress

Single source of truth for phase status + decisions. Plan: `~/.claude/plans/composed-fluttering-crescent.md`.
Gates per phase: ① Spec approved · ② Quality green · ③ Review clean · ④ Accepted.

## Phase status
| Phase | State | Notes |
|---|---|---|
| S0 — de-risk spikes | Spec reviewed → awaiting ① | bundled probes behind `CommanderDebug` flag; needs 1 playtest log |
| P0 — foundation fixes | Spec reviewed → awaiting ① | exclusivity, force-sizing, preview-who, two rings, toggle clarity, occlusion |
| P0.5 — sandbox + terrain | Backlog | depends on S0 terrain probe |
| P1 — squads + operations + brain | Backlog | depends on S0 unit-id |
| P2 — combined-arms sequencing | Backlog | |
| P3 — economy/production | Backlog | depends on S0 convoy spike |
| P4 — intel board + reports | Backlog | |
| P5 — autonomy UI + HUD | Backlog | |

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
