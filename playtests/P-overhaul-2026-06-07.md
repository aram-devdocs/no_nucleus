# Playtest worksheet — overnight overhaul (2026-06-07)

Everything below was built + headless-verified + screenshot-verified by the overnight loop, but **feel /
look / fun** can only be confirmed by you playing. Launch the sandbox (`scripts/run.ps1`), join a faction in
"Nucleus Dynamic Warfare", and check each item. Screenshots referenced live in `artifacts/screenshots/`.

Legend: [ ] to verify · screenshots are the loop's evidence, your eyes are the final gate.

## WS0 — Visual harness (tooling, not gameplay)
- [ ] Not player-facing. To reproduce the loop's evidence: `pwsh scripts/visual-probe.ps1 -Tag check`
      then view `artifacts/screenshots/check/*-panel.png` / `*-map.png`.

## WS1 — AI fields a mix of objective kinds
- Evidence: `artifacts/screenshots/ws1-verify/03-cmd-panel.png` — CMD list shows 2× CapturePoint + 1× DestroyTarget (was 3× DestroyTarget).
- [ ] Open CMD panel in a live game: confirm the AI's objective list is varied (Capture/Destroy, and DefendArea when your base is threatened), not all "DestroyTarget".
- [ ] Provoke a threat near your HQ and confirm the AI spawns a DEFEND objective there.
- [ ] Confirm capture operations actually progress (air superiority → soften → assault) rather than stalling.

## WS3 — AI narration / barks
- Evidence: `artifacts/screenshots/ws3-verify/03-cmd-panel.png` — CMD panel now has a FEED listing narrated AI ops.
- [ ] Open CMD (or WAR) in a live game and confirm the feed reads in plain language: "AI: capture E 12km", "DestroyTarget … moving in", phase reasons like "softening the target — ground holding".
- [ ] Confirm the feed doesn't spam duplicate lines and updates as the AI makes new decisions.
- [ ] Confirm phase-reason lines help you understand why ground units hold back during SEAD/Strike.

## WS6 — Map selection legibility
- Evidence: `artifacts/screenshots/ws6-verify/03cmd-selected-map.png` — selected objective shows an intent header + status-colored squad lines.
- [ ] Click an objective on the map; confirm a header appears (kind, phase, threat incl. SAM count, priority, owner, squad count).
- [ ] Confirm lines to assigned squads are colored by status (engaged=red, en route=cyan) and each squad cluster has a label.
- [ ] Edge case to eyeball: when the objective is near the map border the header can clip — flag if it bothers you (clamp is a known follow-up).

## WS5 — In-flight HUD
- Evidence: `artifacts/screenshots/ws5-hud2/01-inflight-hud.png` — bottom-right HUD while flying.
- [ ] Fly (map closed) and confirm the bottom-right HUD lists active ops (kind/phase/squads/priority), highlights the top one, and shows the AI intent line.
- [ ] Press H — confirm it hides/shows. Confirm it never blocks clicking/aiming through it.
- [ ] Confirm it disappears when you open the full map (no duplicate info) and reappears when you close it.
- [ ] FOLLOW-UP to sanity-check: does the war/economy actually advance while you fly with the map closed? (The host tick is map-driven; flagged as a possible "tick the war in flight" fix.)

<!-- The loop appends one block per workstream below as it completes them. -->



