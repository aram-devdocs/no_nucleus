# Polish pass — existing UI & functionality

Self-review of the live game (artifacts/screenshots/review-current/, 2026-06-07 ~06:34 MDT). The overnight
overhaul made the UI legible + the AI smart; this pass makes the existing surfaces **coherent and polished**.
Same loop: branch per item off `auto/overnight` → check → audit PASS → visual-probe + READ before/after crops
→ self-reviewed PR → squash-merge → ledger. Each item is screenshot-verified (judged on "does it look better").

## Findings (from the screenshots)
- **Inconsistent buttons** — palette (CAPTURE…), SELECT/ASSIGN, and the big green toggles are three different
  visual languages (size/color/weight). No unified button.
- **Incoherent color semantics** — ASSIGN is magenta (clashes); the SELECTED objective row is **red** (reads as
  an error, not a selection); accent (cyan) vs faction-blue vs green toggles aren't reconciled.
- **Flat hierarchy** — every section header is the same small accent text; OBJECTIVES / editor / ASSIGN /
  COMMANDER run together with no dividers or breathing room → hard to scan.
- **Dense "·" row text** — `CapturePoint · Strike · P5.0 · 2 sq [AI]` is tiny and separator-heavy; no
  at-a-glance kind cue.
- **Map labels float** — objective labels have no contrast backing; can wash out over terrain.
- **HUD is plain** — readable but structureless (no accent edge, no per-row kind cue).
- Strongest surface today: the WAR attrition bars (blue/red) — use that level of structure elsewhere.

## Workstreams (ROI-ordered; all screenshot-verified)
- **PWS1 — Coherent color + button system.** Theme semantic colors (Accent=cyan, On=green, Danger=red ONLY for
  destructive, Selected=Accent); one consistent `UiFactory.Button` look (height/label/idle/active using the
  captured native button sprite). Recolor: ASSIGN magenta→Accent; selected objective red→Accent; REMOVE stays
  the only red. Files: `libs/Nucleus.Ui/Theme.cs`, `UiFactory.cs`, `CommanderPanel.cs`. (highest leverage)
- **PWS2 — Visual hierarchy.** Add `UiFactory.Divider()` + a stronger section-header style; insert dividers +
  consistent spacing between OBJECTIVES / EDITOR / ASSIGN / COMMANDER / BUILD / SQUADS / FEED.
- **PWS3 — Row legibility.** A small kind-colored dot on objective/op rows (CMD/WAR) + HUD rows; tidy the
  separator soup; align fields. Reuse `ObjectiveColor`.
- **PWS4 — Map label contrast.** A subtle dark pill behind objective labels (MapOverlay) so they read over any
  terrain; nudge marker/label offset.
- **PWS5 — HUD styling.** Accent top rule + per-row kind dot + tighter alignment in `FlightHud`.

## Guardrails (unchanged)
Merge only to `auto/overnight`; full `audit.ps1` PASS per item; arch canary (Ui lib free of Nucleus.Squads);
new tests only; no publish/tag/admin. Verify every item by reading the before/after crops. Fold results into
the consolidated PR #12.
