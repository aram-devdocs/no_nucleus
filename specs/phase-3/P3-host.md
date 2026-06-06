# Spec — Phase 3: Host/Platform + IMod contract + Commander as first mod

**North-star ref:** "Platform: a mod loader managing multiple mods, each with its own in-game button."
**Riskiest phase** (Canvas/tick/patch ownership inversion). Behavior-preserving: the live game must look and
behave exactly as today after each sub-step.

## Goal
Introduce the host/loader architecture *inside the existing plugin* (no new plugin DLL yet — de-risks the
ownership inversion): a `Nucleus.Abstractions` contract (`IMod`/`IModContext`/...), an in-process host that
owns the single overlay Canvas, the tick pump, the three contended Harmony patches, native-asset capture, the
shared game services, and a button registry; and Commander re-homed as the first `IMod` registered into the
host. The plugin stays `CommanderLayer.dll`. Splitting Build/Squad into *separate* plugins is Phase 4–5.

## Sub-steps (each ends green; gate = scripts/audit.ps1 PASS + behavior identical)
- **P3a — `libs/Nucleus.Abstractions`** (this iteration): the contract. Interfaces + small data types:
  `IMod`, `IModContext`, `IModUi`, `IGameServices`, `IButtonRegistry`, `IModTickContext`, `ILogSink`,
  `ModInfo`, `MapButtonSpec`, `MenuItemSpec`, `ModPlatform` (static `Register`). References Domain (UnitView/
  EnemyView/Vec3/UnitTask/FactionInfo/IMapProjection) + Ui (Theme) + Unity (RectTransform/Transform/Sprite/
  Color/Action). Arch allow-list: Abstractions → {Domain, Ui}. Compiles standalone; no behavior yet.
- **P3b — in-process host** (`Nucleus.Platform` host code, initially compiled into the plugin): a `ModHost`
  owning the single Canvas (from CommanderRuntime.EnsureCanvas), the tick pump, native capture, the shared
  game services (one GameRoster/GameIntel/GameUnitCommands/GameProductionService), a `ModRegistry`, and a
  button registry that arbitrates blank VirtualMFD bezel slots. The three contended patches
  (DynamicMap.Update / VirtualMFD.onMapMaximized / MainMenu.Start) call into the host, not CommanderRuntime.
- **P3c — Commander as `IMod`**: wrap the current CommanderService + 4 commander panels + AircraftTaskingPatch
  as a `CommanderMod : IMod` that registers a "CMD" MapButtonSpec, creates its UI layer, and ticks via the
  host. CommanderRuntime's responsibilities split: Canvas/tick/patches/services → host; screen wiring → mod.
- **P3d — loader UI**: the MainMenu "MODS" button → a panel listing registered mods with an enable toggle
  (persisted to BepInEx config); toggling shows/hides the mod's layer + bezel button.

## Acceptance criteria (whole phase)
- Solution builds 0 warnings; Core 118 + arch (+ Abstractions non-vacuous) + contract 11 green.
- New integration tests (Nucleus.Integration.Tests, P3b+) drive host lifecycle headlessly with a FakeGame:
  register → Initialize → Tick → enable/disable; distinct bezel slots; one shared ProductionQueue; AUTO +
  manual buys don't double-purchase.
- In-game (playtest, end of phase): CMD button still attaches + toggles the panel; manual orders, AUTO
  commander, production, squads behave exactly as before; the new "MODS" menu lists Commander and toggles it.

## P3a acceptance (this iteration)
- `libs/Nucleus.Abstractions` builds (netstandard2.1, imports GameReferences for Unity types).
- Arch test: Abstractions present, references only {Domain, Ui} among Nucleus libs (add a synthetic-safe rule update).
- `scripts/audit.ps1` → AUDIT PASS. No wiring into the plugin yet (contract only), so behavior unchanged.

## Risks / decisions
- **Single-plugin-initially** avoids a load-order boundary during the riskiest inversion (decision logged).
- **Abstractions → Ui** (for the Theme type in IModUi) — acceptable; Ui never references Abstractions (no cycle).
- **Shared live state** (ProductionQueue/SquadRoster/CommanderState) becomes host-owned singletons injected via
  IGameServices/context, not cross-object reach-ins — preserved exactly (no behavior change).
- Tick ORDER inside CommanderService (manual reissue → brain → auto-buy → drain → air-intent) must be preserved.

## Estimated commits: 4–6 (one per sub-step + fixes)
