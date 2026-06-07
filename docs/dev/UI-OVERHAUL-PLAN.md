# UI/UX + gameplay-loop overhaul — self-directed

Mandate (user): stop fixing only the named bit — take the WHOLE UI/UX and gameplay loops, self-audit, and iterate
autonomously. Two read-only audits done (bezel/MFD bug; UI atoms/layout/scroll/DRY). This plan drives a DRY,
atomic, responsive, scrollable, click-for-detail UI facelift PLUS a gameplay-loop audit. Same loop: branch per
item off `auto/overnight` → `check.ps1` → `audit.ps1` PASS → `visual-probe.ps1` + READ before/after crops →
self-reviewed PR → squash-merge → ledger. Each item screenshot-verified ("clearly better, or revert").

## Audit findings (concrete)
- **Bezel bug**: each Nucleus bezel button is tinted green only inside its own onClick; when a 2nd same-side
  button is pressed the game closes the 1st screen (`VirtualMFD.PressLeftButton`→`HideAllLeftScreens`) but never
  re-runs the 1st button's handler → it stays green. Fix: per-frame refresh each bezel button's color from its
  paired `MFDScreen.isActive` (in `ModHost.Tick`, which runs while the map is up).
- **Sizing**: `UiFactory.Button` pins no height; layouts `childForceExpandHeight=true`; rows pin width but not
  height → buttons/rows jitter. Fix: `UiTokens` (sizing constants) + `ButtonFixed` (pinned height) + consistent row height.
- **Scroll**: a `ScrollRect` exists on CommanderPanel but no visible **Scrollbar** is wired and ModPanel has no
  scroll at all. Fix: `MakeScrollable` helper (ScrollRect + ContentSizeFitter + visible Scrollbar).
- **DRY**: row pooling/render duplicated 3–4× (EnsureEntityRows/EnsureOpRows/EnsureRows). Fix: one generic list/row helper.
- **Detail**: rows are flat; only objectives have an editor. Fix: generalize a select→detail affordance.
- **ModPanel**: hardcoded 460×880, not sized to the MFD screen. Fix: configurable/responsive size.

## Workstreams (ROI-ordered; self-paced)
- **UO1 — Bezel tint fix** (the reported bug). `HostButtons.RefreshTints()` from `MFDScreen.isActive`, called in `ModHost.Tick`.
- **UO2 — UiTokens + ButtonFixed**: centralize sizing; pin button/row heights so the panel is even.
- **UO3 — Working scroll**: ScrollRect + visible Scrollbar in ModPanel (all panels scroll), ContentSizeFitter.
- **UO4 — DRY rows**: one pooled list/row helper; migrate squads/ops/build/objectives onto it.
- **UO5 — Facelift pass**: apply tokens/theme consistently (spacing, alignment, button variants) — cohesive look.
- **UO6 — ModPanel responsive sizing** to the MFD screen.
- **GL1 — Gameplay-loop audit**: investigate + fix where safe — (a) does the war/brain advance while flying
  (host ticks off DynamicMap.Update — flagged in WS5)? (b) enemy-AI driver gating (WS2); (c) objective→squad→
  execute loop clarity/fun. Fix the safe parts, flag the rest for the human.

## Guardrails (unchanged)
Merge only to `auto/overnight`; full `audit.ps1` PASS per item; arch canary (Ui lib free of Nucleus.Squads);
determinism canary; new tests only; no publish/tag/admin. Verify every visual change by reading the crops.
