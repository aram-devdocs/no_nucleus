# Spec — S0 (de-risk spikes) + P0 (foundation fixes)

Status: **awaiting Spec Gate ① sign-off** (Plan-agent review + user approval) before implementation.

---

## P0 — Foundation fixes (make tasking usable + smart)

### Goal
Fix the five playtest defects + the core "not smart" logic so manual tasking feels deliberate. This is also the bottom rung of the autonomy ladder (it stays as the future "Manual" path).

### Player feels
Hover an armed order → I see *exactly which units* will respond (lines + names) inside a clear **inner (area-of-operations) + outer (unit pull) ring**; legible AIR/LAND/SEA checkboxes; orders never double-book a unit; it sends *enough*, not everyone; the panel no longer covers the map.

### Scope
IN: cross-order exclusivity; force-sizing-to-threat; preview-who (map lines + panel names); two dashed rings; checkbox-style domain toggles; stop occluding the map. OUT: squads/operations/brain (P1+); any new game API.

### Files (changed)
- `src/Core/Planning/OrderPlanner.cs` — `SelectUnits(order, roster, cfg, threat, excludeIds)` + `RequiredForce(threat, kind, doctrineRatio)`.
- `src/Core/Planning/AssignmentManager.cs` — global committed-units set; exclude on Add/reassign; release on clear/complete.
- `src/Core/Model/Orders.cs` — `CommanderConfig` gains `ForceRatio` (1.5), `MinForce` (1). *(No `AoRadius` — the inner ring binds to the real `ThreatRadius`, see below.)*
- `src/Game/CommanderService.cs` — pass exclusions; expose preview unit positions.
- `src/Ui/MapOverlay.cs` — `SetHover` draws outer+inner ring + faint lines to each previewed unit; `UiFactory` dashed ring.
- `src/Ui/UiFactory.cs` — `DashedRingSprite()`; checkbox atom.
- `src/Ui/CommanderPanel.cs` — checkbox-style toggles (single accent, clear ON/OFF); status lists previewed unit names.
- `src/Composition/CommanderRuntime.cs` / `src/Ui/CommanderMapScreen.cs` — lower canvas sort to sit above game HUD but dock panel to a corner; ensure overlay markers don't bury unit icons (semi-transparent, below-icon order).

### Interfaces & data shapes  *(revised per spec review)*
```
OrderPlanner.SelectUnits(CommanderOrder, roster, CommanderConfig, ThreatPicture=null,
                         IReadOnlyCollection<string> excludeIds=null) : IReadOnlyList<UnitView>
OrderPlanner.Plan(order, roster, threat, cfg, excludeIds=null)        // <-- excludeIds threads through the REAL call site
OrderPlanner.RequiredForce(ThreatPicture, OrderKind, CommanderConfig) : int
   // Attack/Defend: clamp(ceil(threat.Count * ForceRatio), MinForce, MaxUnitsPerOrder)  (threat==null -> MinForce)
   // Move/Capture/Resupply/Build: MaxUnitsPerOrder cap only (threat-independent) — the suitable filter already bounds them
```
**Committed-units set is DERIVED, not hand-maintained** (review's blocking fix): each Tick (and on Place), `AssignmentManager` recomputes `Committed = union of AssignedUnitIds over orders whose Status==Active`, *intersected with the live roster's alive ids*. So dead units, Failed orders, completed orders, Clear and ClearAll all release **for free** — no per-site add/remove to leak. Keyed on `persistentID` (revisit if the S0 UID probe shows id reuse).
- `SelectUnits`/`Plan` for order O exclude `Committed \ O.AssignedUnitIds` (a unit may stay on its own order).
- **Preview path** (`CommanderService.PreviewAt` → `OrderPlanner.Preview`) passes the same exclusion so the hover honestly shows only *available* units.
- `AssignmentPreview.Assignable` (UnitView[] with Position+Name) already exists → overlay draws pooled lines to each, panel lists names.

### Pure logic to unit-test (Core)
1. A unit committed to Active order A is **excluded** from order B's selection.
2. Clearing A (and ClearAll) frees its units for B.
3. A committed unit that **dies** (drops from roster) frees its commitment next tick (derived-set reconcile).
4. A committed unit on a **Failed/Complete** order is released.
5. `RequiredForce` Attack: 0 threat → MinForce; N threat → clamp(ceil(N×1.5), MinForce, MaxUnitsPerOrder).
6. `RequiredForce` non-Attack (e.g. Resupply): threat-independent, ≤ MaxUnitsPerOrder.
7. Selection returns ≤ RequiredForce (not blind Take(Max)).
8. Reassignment on loss (`Tick`→`Plan`) cannot poach a unit committed elsewhere.
9. `PreviewAt` honors the committed exclusion (hover shows only available units).

### Acceptance
- Core tests above green; build clean; GameContract green.
- Playtest checklist: toggles legibly ON/OFF; hover shows unit lines + names + two rings; placing two overlapping orders never shares a unit; an order on light resistance sends a few (not 6); panel doesn't cover the map center.

### Runtime checks
Bundled into the **single S0 playtest** (below): the user opens the map, arms an order, hovers, places two overlapping orders, and pastes the log + a screenshot.

### Risks & open decisions  *(updated per spec review)*
- **Inner ring = `ThreatRadius` (3000m), a real threshold** the order assesses threat over — not a cosmetic invented radius. Outer = pull/selection radius. Both get **independent** min/max pixel clamps (they diverge at zoom: 3000m vs 6000m).
- Ring size derives from `mapDisplayFactor` (a fractional local-unit scalar, not pixels) — clamp each ring's local size so neither collapses/explodes across zoom.
- **Occlusion is two separate surfaces, not one sort fix:** (a) the *panel* lives on the mod `_canvas` — dock it to a corner; lowering `sortingOrder` risks it falling behind a fullscreen game element and losing raycasts, so verify pointer still hits it (the pan-guard depends on `IsPointerOverGameObject`). (b) the *order markers/lines* live on the game's `map.iconLayer` — keep them from burying unit icons via sibling order / smaller / semi-transparent, **not** via canvas sort.
- Force-sizing = "start simple" clamp; threat-sizing applies to **Attack/Defend only**; tune after playtest.
- Committed set keyed on `persistentID`, reconciled vs live roster each tick — revisit if S0 UID probe shows reuse.
- Occlusion + rings are UI → verified by playtest, not tests.

### Estimated commits
~6: (1) derived committed-set exclusivity + tests (commit/clear/death/failed/preview), (2) force-sizing per-kind + tests, (3) preview pooled lines + panel names, (4) two dashed rings (independent clamps, inner=ThreatRadius), (5) checkbox toggles, (6) occlusion/dock (panel raycast verified, iconLayer marker alpha) + gate-summary.

---

## S0 — De-risk spikes (one bundled playtest)

### Goal
Resolve the runtime unknowns that gate later phases, in **one** playtest, behind a `CommanderDebug` config flag (default off) that logs structured `=== probe ===` lines.

### Files (new, throwaway/foldable)
- `src/Game/CommanderDebugProbe.cs` — all probes in one place, called from `CommanderService.Tick` when `Plugin.CommanderDebug` is on.
- `src/Plugin.cs` — bind `CommanderDebug` config bool.
- (maybe) `tools/CommanderLayer.CodeGen` — offline dump of `ConvoyGroup` members if the API exposes them.

### Probes (each logs a tagged line)
- `KILL`: each tick, for tracked enemies, log id + `disabled` + present-in-trackingDatabase. (Does a kill prune it, and when?)
- `UID`: log friendly unit `persistentID`s once + on change; confirm stable, non-reused after death.
- `CONVOY`: on a manual Build/Commission, log the convoy name + members (if reachable) + where new friendly ids appear.
- `TERRAIN`: sample terrain height + water test at cursor and a coarse grid; log water/land. **Doubles as the P0.5 coordinate source.**
- `AIR`: with `EnableAircraftTasking` on, log `NoTarget` firings + whether idle aircraft approach the zone.

### Acceptance
PROGRESS.md S0 table filled Resolved/Partial/Deferred from the log; later specs cite it. No code depends on S0 output yet — it only *informs* P0.5/P1/P3.

### Risks
Terrain API may need a specific raycast layer / `TerrainCollider`; probe logs whichever method works. Convoy contents may be private/empty → fall back to a tuned name→roles table (recorded as the P3 open decision).

### Estimated commits
~2: probe + config bind; (post-playtest) findings write-up in PROGRESS.md.

---

## Sequencing
Build **P0 pure-logic first** (exclusivity + force-sizing — fully testable now, no playtest), then P0 UI, then bundle the S0 probes into the same build so the **one** playtest verifies P0 UI *and* collects S0 data. Commit each green step.
