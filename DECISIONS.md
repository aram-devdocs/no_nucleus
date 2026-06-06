# DECISIONS — Nucleus

> Decision log + per-phase retros. Durable lessons also go to the agent's persistent memory.
> Append-only; newest at top of each section.

## Architecture decisions
- **2026-06-06 · Shared campaign-domain lib, not cross-plugin calls.** AUTO brain stays a pure
  `CommanderBrain.Tick(snapshot, state)` calling down into `Nucleus.Squads`/`Nucleus.Production`;
  never a runtime call into the Squad/Build plugins (BepInEx load-order/absence fragility). Human and
  brain operate the same domain types + the same host-owned live state.
- **2026-06-06 · Single host owns Canvas/tick/contended-patches.** `Nucleus.Platform` is the only plugin
  patching `DynamicMap.Update` / `VirtualMFD.VirtualMFD_onMapMaximized` / `MainMenu.Start`; mods register
  via `IMod` + `[BepInDependency(HardDependency)]` (explicit registration, not reflection scan).
- **2026-06-06 · Namespaces frozen at `CommanderLayer.*` until Phase 7.** Folder/assembly ≠ namespace in C#;
  do the structural split first, rename mechanically last — keeps every step low-churn and test-green.
- **2026-06-06 · Brand = Nucleus; repo+folder = `no_nucleus`; distribution = NuGet (SDK) + Thunderstore +
  native loader + source + Nexus + Steam Workshop mission "Nucleus Dynamic Warfare".** (User decisions.)

## Pending decisions (options + recommended default; escalate before the gated action)
- **Repo + folder rename** (`commander` → `no_nucleus`): irreversible-ish outward action. Default: do
  `gh repo rename no_nucleus` at Phase 7, hand the human the local folder-rename steps. **Park for explicit go.**
- **Publishing** NuGet / Thunderstore / Steam Workshop: requires accounts + secrets. Default: prepare
  packages + `docs/DEPLOYMENT.md`, park for the human to create accounts/secrets and approve first publish.

## Per-phase retros
_(appended at each phase close)_
