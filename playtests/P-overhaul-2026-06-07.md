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

## WS7 — Squad legibility
- Evidence: `artifacts/screenshots/ws7-verify/05-sqd-panel.png` — rows show composition.
- [ ] Open SQD: confirm each row reads as composition ("4× MBT, 1× IFV") + what it's doing (e.g. "CapturePoint — Strike" / "Ready").
- [ ] Confirm a depleted squad shows in red and (if under target) a have/need count like "(2/4)".
- [ ] Deferred: there's no "assign this squad to that objective" button yet (AUTO/MANUAL toggle is the current control). Tell me if you want explicit assignment next.

## WS8 — Build clarity
- Evidence: `artifacts/screenshots/ws8-bld2/04-bld-panel.png`.
- [ ] Open BLD: confirm the "AIRCRAFT — spawn from your airbases" note and the "Funds · Queued · After" line.
- [ ] Queue a couple of convoys; confirm Queued rises and After drops (red if it would go negative).
- [ ] After a convoy actually dispatches, confirm a "Convoy dispatched: … arriving at the front" line appears in the feed (CMD/WAR).

## WS2 — Commander personalities (genomes)
- Headless-proven (6 genome tests + determinism canary). Foundation for evolving AI (WS11).
- [ ] In a live war where the enemy is AI-driven, check the BepInEx log for `[NUCLEUS:METRIC] enemy-commander archetype=…` — confirm distinct archetypes per faction across runs.
- [ ] FOLLOW-UP: the enemy-AI driver (DriveEnemyAi) didn't execute in the autoloaded+joined harness scenario — confirm whether the enemy commander actually runs our brain in real play, or if WarSetup/native-AI gating prevents it (this also affects whether the enemy "feels" personalized). Possibly the same "war advances only with map open" tick issue from WS5.

## WS9 — Visual polish
- Evidence: `artifacts/screenshots/ws9-verify/02-map-open-map.png` (markers/labels), `01-inflight-hud.png` (HUD contrast).
- [ ] Confirm map objective markers + labels read clearly at normal zoom.
- [ ] Confirm the in-flight HUD is legible over bright sky (higher opacity).
- [ ] Bigger picture: the panels are still plain MFD chrome — tell me if you want a fuller native-UI restyle (deferred as higher-risk).

## WS11 — Genome self-play / evolution (infrastructure)
- Headless-only. Run `pwsh scripts/evolve.ps1` → `artifacts/genomes/{genepool.tsv,report.txt}`; sample at `docs/dev/evolve-report-sample.txt`.
- [ ] (Optional) Run it and skim the report. NOTE: fitness is currently flat (ties) on the symmetric scenario — the GA can't differentiate yet; this is infrastructure + an honest baseline, not a tuned result. Evolved genomes are NOT used in-game (hand-authored archetypes ship).

## WS12 — Squad assign UX + header clamp
- Evidence: `artifacts/screenshots/ws12-verify/03cmd-selected-panel.png` (ASSIGN list), `03cmd-selected-map.png` (header not clipped).
- [ ] Select an objective in CMD; confirm an "ASSIGN FORCE" list of free suitable squads appears with ASSIGN buttons.
- [ ] Click ASSIGN on a squad; confirm it gets tasked to that objective (appears as engaged / moving to it; an operation opens).
- [ ] Select an objective near the map's bottom/right edge; confirm its info header stays fully on-screen (no clipping).









