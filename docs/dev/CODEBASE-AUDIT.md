# Codebase hardening loop — enterprise pass (DRY / SSOT / thin apps / packages)

Self-directed /loop while the user games. HEADLESS-ONLY: compile-check the whole solution with the deploy
disabled (`dotnet build Nucleus.sln -c Release -p:TreatWarningsAsErrors=true -p:Sandbox=C:\__nodeploy__` — the
DeployPlugin target is gated on `Exists($(Sandbox))`, so a non-existent Sandbox skips the copy into the
sandbox game the user is playing); run headless tests (Core/Sim/Arch/Contract/LogAudit/Installer/Integration);
commit LOCALLY (pre-commit is headless). NO pushes, NO game launches until the user says they're free.

## Package graph (acyclic — guarded by Nucleus.Architecture.Tests)
libs: Abstractions · Domain(leaf) · Squads→Domain · Production→Domain · Campaign→{Domain,Squads,Production} ·
Sim→{Domain,Campaign} · GameSdk→{Domain,Squads,Production,Campaign} · Ui→{Domain,Production,Campaign}
apps (thin wrappers): Platform · Commander · Build · Squad · Warfare   sdk: Sdk · ModTemplate   tools: CodeGen · Evolve · Installer · LogAudit

## Work-list (one per loop iteration; headless-verified + local commit)
- [CB1] SSOT objective visuals: one `Nucleus.Ui.ObjectiveVisuals` (color/tag/name, hex DERIVED from color) →
  adopt in CommanderPanel + MapOverlay + FlightHud; delete the 3 duplicate kind→color/tag/name switches.  ← NOW
- [CB2] SSOT role/family labels: ensure `RoleLabels` (Domain) is the only place; HqView/UI use it; no dup.
- [CB3] DRY row pooling: collapse EnsureEntityRows/EnsureOpRows/EnsureRows into one generic pooled-row helper.
- [CB4] Thin the apps: move any non-wiring logic out of apps/* into the right lib (apps = composition only).
- [CB5] Packages for other devs: per-lib package metadata (IsPackable, id/desc/authors), README, clean public API.
- [CB6] Native SDK usage: route all game access through GameSdk/NativeAssets/NativeUi — no stray reflection/magic
  strings in apps; widen NativeUi adoption.
- [CB7] Arch hygiene: add Nucleus.Sim to the arch allow-list explicitly; confirm no-cycles + Unity-free purity.
- [CB8] Public-surface review: internal-by-default; XML docs on public APIs.

## Status
- CB1: DONE (local) — ObjectiveVisuals SSOT; CommanderPanel/MapOverlay/FlightHud adopt it; 3 dup switches deleted;
  hex derived from color (no skew). Full-sln compile (deploy off) + arch 9 / core 137 / sim 41 PASS.
- CB2: DONE-clean — RoleLabels.Short (Domain) is already the SOLE role→label source (one call site, HqView); no dup to remove.
- CB7: DONE (local) — Nucleus.Sim added to the arch allow-list ({Domain,Squads,Production,Campaign}, its real
  Campaign closure) + PureLibs (Unity-free asserted). Arch 9/9 PASS. (Canary caught the transitive Squads ref → declared it.)
- Next: CB3 (DRY row pooling), then CB4/CB5/CB6/CB8.
