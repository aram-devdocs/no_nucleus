# Design system

The UI has one source of truth for every visual constant. Surfaces compose tokens; they never hardcode.

- **Colors** come from `Theme` (mod chrome + semantic cues), `NativeColors` (game affiliation, captured at
  runtime), or `ObjectiveVisuals` (objective-kind colors). A raw `new UnityEngine.Color(...)` is allowed ONLY
  in those definition types plus the procedural-sprite/border builders (`UiFactory`, `NativeUi`). Enforced by
  `DesignSystemValidator` (Cecil-scans UI assemblies for `Color::.ctor` outside the palette types).
- **Sizes / spacing / fonts** come from `UiTokens` (button/row heights, panel footprints, marker/label sizes,
  font sizes). No magic pixel or font-size literals in layout code.
- **User-facing strings** (section headers, hints, empty states) live in `UiStrings`; objective/phase wording
  lives in `ObjectiveText` (pure Domain SSOT, guarded by `SsotValidator`). Per-row dynamic text is built by the
  renderer from the read-model.

## Color semantics (load-bearing)

- **`Theme.Active` (green)** = the single on / selected / active cue (toggles ON, selected objective, "go").
- **`Theme.Danger` (red)** = destructive only (REMOVE). Red never means selected.
- **`Theme.Accent` (faction)** = identity + primary call-to-action (START WAR).
- **`Theme.ButtonIdle` (gray)** = off / unavailable.

Keep `green = AI-on` consistent: the AI COMMANDER / AUTO-FILL toggles and the per-op/per-squad AI state all use
`Active` for "AI is handling it". Do not invert one without the others.

## Statelessness

`Nucleus.Ui` types render from immutable snapshots/VMs and hold widget handles + theme + UI-local selection
only — never a reference to the live, mutable campaign model. Enforced by `UiStatelessnessValidator`.
