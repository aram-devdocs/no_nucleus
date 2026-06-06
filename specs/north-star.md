# North star — Nucleus Dynamic Warfare

> The experience every phase builds toward. Each work-item's spec must map to a bullet here, or be logged
> in BACKLOG "Discovered" as an explicit scope-add. The loop re-checks this each phase to resist drift.

## Vision
A **DUWS-style dynamic campaign** (ARMA's Dynamic Universal War System) crossed with an **RTS layer**, on top
of Nuclear Option. The player creates units, assembles squads, builds bases/forces, and commands a **dynamic
combined-arms war** — **solo with an AI-assisted commander** or **co-op with friends**. Command is
**Majesty-style / indirect**: set objectives, doctrine, incentives; AI-driven forces execute. The war is
**persistent and long-lived** (save/resume a multi-hour campaign) with **both factions** running commanders
that build and fight. Shipped as a Steam Workshop mission over the Nucleus mod stack.

## Capability checklist (maps phases → experience)
- [ ] **Platform**: a mod loader managing multiple mods, each with its own in-game button (Phase 3).
- [ ] **Create units / build**: purchase vehicles/bases/units from a Build UI; see queue + ETA (Phase 4).
- [ ] **Assemble squads**: form squads from forces/build output and command them (Phase 5).
- [ ] **Indirect command**: place objectives/doctrine; AI executes combined-arms phases (exists; refine).
- [ ] **AI-assisted solo**: the commander runs your side when you don't micromanage (exists; per-faction-ize).
- [ ] **Dual-faction dynamic war**: BOTH sides run commanders that build + fight (Phase 6).
- [ ] **Persistence**: save and resume a multi-hour campaign (Phase 6).
- [ ] **Co-op**: friends share a side / a campaign (Phase 6+, multiplayer — scope TBD).
- [ ] **Distribution**: SDK on NuGet; mods on Thunderstore/loader/source/Nexus; mission on Steam Workshop (Phase 6–7).

## Non-goals (for now)
- Replacing the native game AI (we work WITH it; commander is opt-in/off by default).
- Full custom map-unit-picker squad creation (covered by "create from a placed order").
