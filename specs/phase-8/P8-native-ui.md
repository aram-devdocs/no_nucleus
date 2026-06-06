# P8 — Native, codegenned UI (replace the overlay approach)

**Playtest feedback (2026-06-06, real in-game run) that triggered this:**
1. The start-menu "MODS" panel is a **custom Canvas overlay** bolted on top of the native menu —
   unresponsive, stacked, wrong. UI must come from the game (codegenned) and be **added into** the
   native menu, not overlaid.
2. No "Nucleus Dynamic Warfare" mission in singleplayer — it was never authored (only Commander Debug).
3. **No map bezel buttons at all** (CMD gone too). Root cause: `HostButtons` reflects into the game's
   private `leftButtons`/`rightButtons` lists, hunts a "blank" slot, and hijacks it — it silently found
   nothing (no `bezel-buttons-attached` in the log), and it's the fragile internals-poking pattern that
   must go.

## Principle
UI is **the game's own widgets, instantiated from the game's prefabs** (single source of truth, responsive
by construction), reached through the **codegenned** typed accessor layer (`Nucleus.GameSdk.Generated`).
No custom Canvas overlays; no reflection slot-hijacking. We *add into* the native MFD + menu systems.

## How the native systems work (from decompiled `VirtualMFD` / `MFDScreen` / `MainMenu`)
- **MFD bezel:** `VirtualMFD` holds parallel `leftButtons[i]` ↔ `leftScreens[i]` (and right). A bezel press
  calls `public PressLeftButton(Button)` → finds the index → toggles the paired `MFDScreen`
  (`ShowScreen`/`CloseScreen`). Buttons with no paired screen are `enabled=false` in `SetupButtons`.
  `onMapMaximized` → `ToggleAllButtons(true)`. The button→screen wiring is a persistent `onClick` listener
  in the prefab calling `PressLeftButton(thisButton)`.
- **MFDScreen** (all public): `displayPanel` (content GO), `label` (Text), `highlight` (Image),
  `Setup(mfd, shortName)`, `ShowScreen(pos)`, `CloseScreen(pos)`, `isActive`.
- **MainMenu:** native buttons (e.g. `missionsButton`) + `overlayMenuLayer`; `SelectMissions()` etc.

## Design
**Native MFD button (replaces HostButtons slot-hijack):** for each registered mod button, **clone** a real
`leftButtons[k]`/`rightButtons[k]` GameObject (keeps native style + layout group → responsive), **clone** a
real `MFDScreen` for its content, append both to the parallel lists at a new index, set the screen's
`displayPanel` to our mod content layer, `Setup(mfd, label)`, then **re-point the clone's `onClick`** to
`mfd.PressLeftButton(clone)` (the cloned persistent listener still references the original button — must be
cleared + re-added in code). Call `SetupButtons()`. Our mod panels render **into the screen's displayPanel**,
not a separate Canvas.

**Native menu entry (replaces MainMenuLoader overlay):** clone the native `missionsButton` into the menu's
button container, label it "NUCLEUS", and open a native screen (a cloned settings-style page) listing the
mods with native toggles. Delete `MainMenuLoader` + the custom-Canvas mod loader.

**Codegen additions:** expose `MainMenu` private fields (`missionsButton`, `overlayMenuLayer`, and the menu
button container) as typed accessors. MFD button/screen fields are already codegenned; MFDScreen members +
the MFD press/setup methods are public (no reflection).

## Sequence (chosen: foundation first, then all UI)
1. **Foundation A — native MFD button** (clone button+screen, append, re-wire onClick). Prove with CMD, then
   BLD/SQD/WAR. Replaces `HostButtons`. *(playtest-iterated)*
2. **Foundation B — codegen MainMenu accessors** + a native menu entry/page; delete the overlay loader.
   *(codegen headless-verifiable; render playtest-iterated)*
3. **Convert mod panels** (Commander/Build/Squad/Warfare) to render into native MFDScreen displayPanels.
4. **Mission** — author + deploy "Nucleus Dynamic Warfare" (issue 2).

## Acceptance (per piece, via playtest + LogAudit)
- `[NUCLEUS:SELFTEST] PASS bezel-buttons-attached` + `bezelButtons=N`, and CMD/BLD/SQD/WAR visible on the map.
- A native "NUCLEUS" item in the main menu opens a native page; no floating overlay.
- "Nucleus Dynamic Warfare" appears in singleplayer missions and loads.
- Headless: build 0w + full gate green; codegen contract test green for the new accessors.
