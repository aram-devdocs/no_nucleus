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
| P1 — squads + operations + brain | ①②③ ✔ · awaiting ④ playtest | brain wired flag-gated (EnableAutoCommander off). Review B1/B2/S1/S2 fixed (5bfefd7): ops complete+free squads, objectives pruned, diff tasking, auto excludes manual-committed. **+ AUTONOMY LADDER COMPLETE: per-op/per-squad Manual (8bae8d8); Assisted = propose+confirm via Proposals/ConfirmProposal (d3e1465); proximity-aware MatchSquads (81f8284).** 118 Core. Deferred: S3 focus-fire TargetId (P4), S4 executor O(n²). ④ = enable flag + observe autonomous slice |
| P2 — combined-arms sequencing | ✅ gates + integration | PhaseGates (924fe32) + operations advance CombatPhase cursor, task per-phase — armor holds while artillery/aircraft soften (07d4fbc). 60 Core. Pure+tested; activation playtest-gated with P1 |
| P3 — economy/production | Backlog | depends on S0 convoy spike |
| P4 — intel board + reports | Backlog | |
| P5 — autonomy UI + HUD | HQ readout ✔ · awaiting ④ playtest | HqView threaded CommanderService.AutoHq → CommanderMapScreen.RenderHq → CommanderPanel: live AUTO COMMANDER header + ops/production/feed body, renders each tick on the open map (0fe6ee3). 113 Core + 11 contract. Autonomy-flip controls + cockpit HUD = later. ④ = see it populate with EnableAutoCommander on |
| P6 — native UI component library | P6.1 ✔ · P6.2 contracts+probe ✔ · render awaiting ④ playtest | **P6.1 DONE (buildable):** codegen `Asset` tag → `src/Game/Generated/NativeAssets.generated.cs` (typed snapshot of GameAssets font/HUD colors/icons) + Capture(); composition captures ONCE → NativeColors(+Neutral)/UiFactory.Font/NativeIcons; Theme+OrderColors marked mod-owned. DRY single-source, no skew (4a05b0c). **P6.2 contracts DONE:** BetterBorder(live, was unguarded)/BaseToggle/BoxToggle/SliderToggle/BetterToggleGroup guarded — 67 contract assertions (08cf5c3). **UI-harvest probe DONE:** [S0:UI] logs what's cloneable (e76730c). **Remaining = PLAYTEST-GATED render:** NativeUi runtime clone + UiFactory re-base behind graceful fallback — needs the [S0:UI] log to target real instances. |

Spec: `specs/phase-S0-P0.md`.

**Loop = supervise parallel agent teams (worktree-isolated), integrate behind the quality gate, never idle.**
NOTE: agent worktrees branch from an OLD commit (~fbbac77) — agents must `git merge --ff-only commander-v2`
first, and ONLY add new files (never edit shared files — stale base would revert my work). I do all shared-
file wiring myself during integration windows.
- Wave 1 ✅ BattleLog (8342de3), Production (761ba20), ThreatBoard (8de2823).
- Wave 2 ✅ TargetPrioritizer (aaf91aa), HqView (175bfcf). **98 Core + 11 contract.**
- Wave 3 ✅ Proposals (4d83397). **Module backlog complete: 6 modules, 108 Core + 11 contract.**
- Module code-review ✅ (no blockers); robustness fixed (2fbd4af): ThreatGroup guard+Members, HomeBase, +3 tests. **111 Core.**
- **WIRING CYCLE (mine, in progress):**
  - ✅ BattleLog → brain emits feed (9c0b130).
  - ✅ ThreatBoard+TargetPrioritizer → brain GenerateObjectives, consolidated clustering, ranked (b0d64b4). 112 Core.
  - ✅ Production needs: brain emits RequiredComposition per unfielded objective (0100074).
  - ✅ Auto-production loop closed: GameProductionService (catalog from convoy groups + CmdPurchaseConvoy)
    + CommanderService plan->queue->drain (ac62a42). **113 Core + 11 contract.**

**🏁 AUTONOMOUS COMMANDER LOOP COMPLETE (code):** intel → ThreatBoard → TargetPrioritizer → objectives →
squads → phase-gated combined-arms operations → tasking → BattleLog feed → production needs → convoy buys.
All behind `EnableAutoCommander` (off). Pure-Core tested, Game adapters contract-verified.

**Remaining (all PLAYTEST-GATED — the assistant cannot run the game):**
- **P5 acceptance:** open map with `EnableAutoCommander` on, confirm the AUTO COMMANDER readout populates.
- **P6.2 render:** build `NativeUi` runtime clone + re-base `UiFactory.Button/Toggle/Border` onto native
  clones behind graceful fallback — must be targeted by the `[S0:UI]` harvest log (which instances are
  live/cloneable), so it needs ONE playtest first. Until then the hand-rolled atoms (now reading native
  font/colors via P6.1) remain the safe default.
- **Autonomy-flip UI + cockpit HUD** (P5 depth): per-op/squad Auto/Assisted/Manual toggles + glanceable
  in-jet summary — UX layered on the now-visible HQ readout; design + playtest.
- **The S0 playtest** (one run) unblocks S0 findings + P0/P0.5 acceptance + the P6.2/HUD render targets.
DestroyTarget TargetId deferred (executor ignores it; id-space mismatch per review S3).

**The entire verifiable-without-the-game backlog is now DONE** (P0–P4 logic + autonomy-ladder Manual +
proximity matching, production loop, P5 HQ readout, P6.1 asset SDK, P6.2 contracts + harvest probe).
All further progress needs a BepInEx playtest log from the user — there are no more buildable requirements
that can be closed by code alone.

**↳ THE ONE PLAYTEST that unblocks everything** (set `Commander/CommanderDebug=true` AND
`EnableAutoCommander=true` in the F1 config, load Commander Debug, open the map, play ~1 min, paste the
full BepInEx log + a screenshot). It resolves: S0 findings (`[S0:UID]`/`[S0:KILL]`/`[S0:TERRAIN]`), P0/P0.5
acceptance, **P5** (does the AUTO COMMANDER readout populate?), and the **`[S0:UI]` harvest data** that
tells the P6.2 render exactly which native components are cloneable. **Blocked on the user** — see the
session summary for the two UX decisions (Assisted confirm-flow; cockpit-HUD footprint) the assistant
won't guess.

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
