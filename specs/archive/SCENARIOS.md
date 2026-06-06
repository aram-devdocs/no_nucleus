# Commander — test scenarios (what works today, and how to test it)

> Reflects the current build (P1 + P1.5). Aircraft tasking and logistics/build are later phases
> (see the plan). Install/refresh the mission: `pwsh ./scripts/install-demo-mission.ps1 -Force`, then
> `./scripts/run.sh`.

## What you can command right now
- **Commandable units = Ships, Ground vehicles, (cruise missiles excluded).** Aircraft are **not** directly
  commandable yet — they're not pulled by orders (that's the P4 aircraft phase). So the demo is **naval**.
- Order types: **Attack** (move-attack an area) and **Defend** (move in + hold/garrison).
- All friendly units start **idle** until you command them.

## The "Commander Debug" mission (naval)
- **You:** fly the Boscali trainer (just to be in the mission + open the map).
- **Your fleet (idle):** `BDF Carrier` (a carrier — excluded from attack/defend by role), `BDF Corvette
  Alpha/Bravo` (near the carrier), `BDF Corvette Charlie` (~5 km out).
- **Enemy:** `PRA Vanguard` (~10 km N) and `PRA Escort` (~12 km NE), holding position.

## Scenarios

### 1. Selective Attack (the core fix)
1. Open the map (**M**) → click **CMD** (a blank bezel button now reads CMD; it turns **green** when open).
2. In the panel: enable **SEA** (disable AIR/LAND), set **Range** wide enough to include your corvettes.
3. Click **Attack**. Move the mouse over the map — a **range ring** follows the cursor and the panel shows
   *"N units will respond."* Click near the enemy group.
4. **Expect:** your 3 corvettes sail to engage; the **carrier stays** (not a combat role). Munitions/the
   carrier are never pulled. The order appears in the list with a live unit count; a colored line links the
   objective to each assigned ship.

### 2. Range matters
- Re-arm Attack and **shrink the range** so Charlie (5 km out) is outside the ring → the preview count drops
  and Charlie isn't tasked. Widen it → Charlie joins. This is the "pull radius," shown live.

### 3. Domain filter
- Arm Attack with **SEA off** → preview says *"no units in range"* (everything you have is a ship) and a red
  ring = can't place. Turn SEA on → units return.

### 4. Defend / garrison
- Arm **Defend**, click on/near the carrier. Corvettes move to cover it and then **hold** position
  (garrison) once they arrive, instead of wandering off.

### 5. Manage orders
- **Per-order ✕** clears one order (its ships go idle); **Clear all** clears everything.
- Lose a ship (let one die) → with auto-reassign, the order pulls another suitable ship if available.
- Kill all known enemies in an Attack area → the order auto-completes ("Area clear").

### 6. UI niceties to verify
- **Drag** the panel by its top bar — the map should **not** pan while you drag.
- Close the map (**M**) — the panel, lines, and ring all **disappear**.

## Not yet (coming in later phases)
- Aircraft missions (CAP/SEAD/strike) via the AI-targeting patch.
- Logistics/resupply, capture, and produce/commission.
- A land map scenario with ground vehicles + airbase (this demo is naval).
