# No legacy code

The codebase reflects what runs **now**, not its history.

## Rules
- No commented-out code, orphan shims, or `TODO: remove later`.
- No dead members: unused private members fail the `TreatWarningsAsErrors` build; unreferenced public/internal
  members are flagged by `scripts/deadcode.ps1` and the dead-code validator. Delete, don't disable.
- No second implementation of a thing that already exists (no parallel subsystems kept alive only by their own
  tests). One source of truth per concern (see SSOT: `ObjectiveVisuals`/`ObjectiveText`, `RoleLabels`,
  `RosterGeometry`).
- Removing a feature means removing its code, its tests, its config binds, and its persisted fields together.
- Save-format changes are deliberate: adjust serializer + reader + persistence tests in one change and keep a
  round-trip test green.
