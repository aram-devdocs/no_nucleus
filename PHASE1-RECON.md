# Phase 1 — Decompile recon report (THE GATE)

Game build-hash **`2fdbba8b7`**. Source: `decompiled/Assembly-CSharp.decompiled.cs` (ilspycmd 8.2,
gitignored). Line numbers below refer to that file. Verdict first, then evidence.

## TL;DR — GO. Milestone 1 is "insert an objective" (no broker required).

The game **already has an assigner**: idle friendly AI units (ships, ground vehicles, and aircraft when
not in combat) autonomously path to the **nearest active objective-with-position belonging to their
faction**. Insert one objective-with-position into the player faction's active list and idle units flow
to it — engaging enemies en route. A per-unit command primitive (`UnitCommand.SetDestination`) also
exists as a precise lever and as the case-(b) fallback. Both are read **live**. This is the best case.

---

## §7.1 — Does an assignment/commander layer exist? → YES (hybrid, leans case a)

`MissionPosition` (static, line 22798) is the assigner-query layer. AI unit code calls it every tick:

```
// ShipAI (≈line 169), GroundVehicle (85829), AIPilot (9920/11152/11836) all do, in effect:
if (!holdPosition && !commandedDestination && MissionPosition.TryGetClosestObjectivePosition(unit, out result))
    unitCommand.SetDestination(result.Position, playerCommand:false);
```

`TryGetClosestObjectivePosition(unit)` (22894) →
`MissionManager.Runner.activeByFaction[unit.NetworkHQ]` (22957/22970) → walks that faction's active
`Objective`s, keeps those implementing `IObjectiveWithPosition` with ≥1 `ObjectivePosition`, returns the
nearest (22899-22917, filter at `DistanceTo` 22919). **So the faction's active-objective list IS the
tasking input the AI consumes.** Priority order per unit: player command (`commandedDestination`) >
hold-position > seek nearest faction objective > idle behavior.

Note this is a *pull* assigner, not a smart distributor: each idle unit independently seeks its own
nearest objective. No load-balancing / "send N units." That's fine for milestone 1 ("≥1 unit acts").

## §7.2 — Objective representation + how to insert one at runtime

- `abstract class Objective : ISaveableReference, IHasFaction` (125775). Abstract members to implement:
  `OnStart()`, `UpdateAndCheck()` (return `false` = never self-complete), `ClientOnlyUpdate()`,
  `WriteObjective(ReadWriteObjective)`, `DataReferenceDestroyed(...)`, `DrawData(DataDrawer)`. Fields:
  `SavedObjective` (struct: `UniqueName, Faction, DisplayName, Hidden, Type, Data[], Outcomes[]`),
  `Status`, `FactionHQ` (settable), `NeedsFaction` (virtual, default false).
- `interface IObjectiveWithPosition { IReadOnlyList<ObjectivePosition> Positions { get; } }` (125747).
- `readonly struct ObjectivePosition { GlobalPosition Position; float? Range; }` (125757).

**Injection (POC plan):** define `class CommanderObjective : Objective, IObjectiveWithPosition` in the mod
(we reference the publicized assembly, so subclassing `Objective` is legal). Implement the abstracts as
no-ops, `UpdateAndCheck()=>false`, `Positions => [ new ObjectivePosition(droppedPoint, range) ]`. Set
`FactionHQ = localPlayer.HQ`. Then register it on the host:

```
MissionManager.Runner.StartObjective(obj, addToActive:true);   // internal -> public via publicizer (23062)
// StartObjective -> AddActiveObjective (23096) -> ActiveObjectives.Add + activeByFaction[hq].Add + obj.OnStart()
```

`MissionManager.Runner` is `public static MissionRunner Runner` (21487). `ActiveObjectives` and
`activeByFaction` are **public** fields (23018/23020) — direct-add is the fallback if `StartObjective`
misbehaves. `MissionRunner.Update()` (23038) ticks active objectives server-side; our `UpdateAndCheck`
returns false so it persists until we remove it (`StopObjective`, 23075).

## §7.3 — Case (a) path confirmed

Add the objective to the faction (above); the assigner (`MissionPosition` consumed by unit AI) picks it
up automatically on the next AI tick. No broker needed. **Case (b) is also available** for precise
control: `((ICommandable)unit).UnitCommand.SetDestination(globalPos, playerCommand:true)` (78967) — this
is exactly what the game's own map UI does (50901-50917) and it sets `commandedDestination` which
overrides objective-seeking.

## §7.4 — Live re-read? → YES, live

`ServerSetDestination` fires `ProcessSetDestination` immediately (79007-79015). Idle-seek runs inside the
unit AI ticks (ship/ground/aircraft state Updates + interval handlers `On1s/2s/5s/10sInterval`). No
respawn needed; a newly-added objective takes effect within ~1 AI tick.

## §7.5 — Enumerating the player's faction units

- Local faction: `GameManager.GetLocalPlayer<Player>(out var p)` (19088) → `p.HQ` (FactionHQ, 20212).
- All units: `UnitRegistry.allUnits` (static `List<Unit>`, 15520/15528). Friendly = `u.NetworkHQ == p.HQ`;
  commandable = `u is ICommandable` (Ship 82165, GroundVehicle 85209, Missile 91271; **Aircraft are NOT
  ICommandable**). Aircraft still respond to objectives via `MissionPosition` when `!EnemyContact`.
- Spatial "near the point": `BattlefieldGrid.GetUnitsInRangeEnumerable(globalPos, range)` (15869).
- HUD/marker reference: `ObjectiveMarker`/`ObjectiveMarkerManager` (52362/52459) already draw objective
  markers — reusable for the draw layer.

## §7.6 — Go / No-go → **GO. Milestone 1 = "insert an objective."**

Insert a faction objective-with-position; the existing assigner moves idle units to it. Build the broker
(`UnitCommand.SetDestination`) only as an optional precision lever, not as the foundation.

## Caveats for Phase 4
- **Host authority:** `SetDestination`/objective mutation must run host-side. `ServerSetDestination` is
  `[Server]`; objective registration touches `MissionManager.Runner` (host). Milestone 1 is
  single-player/host-only, so fine. Multiplayer client propagation (`SetAllRemoteActiveObjectives`,
  network sync of objective names/data, 23126) is a later, separate problem.
- **Aircraft** can't be `SetDestination`-commanded; they follow objectives only when not engaged. The
  POC's "≥1 unit acts" is most reliably demonstrated with a ship or ground vehicle present, or by
  watching aircraft divert toward the objective when no enemy is near.
- `Objective.NeedsFaction`/`SavedObjective.Type` are used in logging/asserts — give the injected
  objective a sane `SavedObjective` (UniqueName + Faction name) to avoid noisy logs.
