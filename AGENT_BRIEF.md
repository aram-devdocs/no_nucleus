# Nuclear Option — commander layer mod

Brief for a Claude Code agent running on a Windows PC with the game installed via Steam.
Read top to bottom before touching anything.

Two hard rules:
- **Never write to the Steam install.** Treat it as read-only. All runtime work happens in a gitignored
  sandbox (section 4).
- **Phase 1 is a gate.** Do not write the POC until you've reported what the decompiler shows (section 7).

---

## 1. What we're building

A runtime mod where the **player places objectives on the map** and the AI handles them — *Majesty*,
not an RTS. You drop "take this point" or "defend this area," the game's AI assigns units and acts; you
watch it unfold. You are not piloting or babysitting individual units.

Objective types, in rough priority:

1. **Move/attack here** — drop a point; AI sends suitable units, engaging on the way.
2. **Attack this** — point at an enemy unit or position; AI prioritizes it.
3. **Defend here** — point or radius; AI holds/patrols.
4. Later: area objectives via a drawn circle, recall, priority weighting.

Individual unit control is an explicit **non-goal** for now. It may return later as an optional toggle,
but it is not the foundation.

### The one assumption everything rests on

"AI auto-handles it" assumes the game has an **assignment layer** — a commander/director that maps a
free-floating objective to units. Two possibilities, and Phase 1 decides which we're in:

- **(a) Assigner exists.** Objectives are faction/side-level and something already distributes units to
  them. Best case: we insert an objective and the game does the rest.
- **(b) No assigner.** Objectives are bound to specific units at mission-design time. Then "auto-handles
  it" is something we build: a thin broker that finds idle/suitable friendly units near the objective and
  sets their task.

Same product either way. The difference is whether we inject into an existing assigner or write the
broker ourselves.

### Why "individual first" is a non-question

The first executable proof is the same in both worlds: **one objective takes effect and at least one
unit acts on it.** That looks individual, but it's just the smallest test of whichever primitive exists.

---

## 2. Environment facts (verified from public sources)

- Game: **Nuclear Option**, Steam App ID **2168680**, dev **Shockfront Studios**. Early access, updated often.
- Engine: **Unity, Mono backend** (not IL2CPP) — clean decompilation, standard runtime patching.
- Modding stack: **BepInEx 5 (Mono)** + **HarmonyX**. No official code-mod API; this is the community path.
- The **mission editor presets objectives at mission start** and gives no live tasking — which is exactly
  why this is a code mod.
- In-game config UI: **BepInEx.ConfigurationManager** (F1). Hang all tunables off it.

### Architecture lead

Community reports say aircraft and game objects are **authoritative on the host**, and point to a class
named **`NetworkMission`** as where mission/unit state lives. Orders are a gameplay action and must run
host-side. Milestone 1 is single-player / host-only, which sidesteps this.

---

## 3. Reference material (read before reinventing)

- **mkualquiera/MKModsNO** — https://github.com/mkualquiera/MKModsNO — MIT. `.sln`/`.csproj` with the
  game path wired in, builds with `dotnet build`, hooks the **target-selection** and **HUD** systems.
  Target enumeration is how you "list friendly units" and "read what the player is pointing at." Read first.
- **nikkorap/NuclearMods** — https://github.com/nikkorap/NuclearMods — Built against v0.31. Note the
  `Host+Client` vs `clientside` split — the host-authority boundary made concrete.

Tooling and docs:
- BepInEx install — https://docs.bepinex.dev/articles/user_guide/installation/index.html (BepInEx 5 x64 Mono)
- ConfigurationManager — https://github.com/BepInEx/BepInEx.ConfigurationManager
- HarmonyX — https://harmony.pardeike.net/
- ILSpy — https://github.com/icsharpcode/ILSpy / dnSpyEx — https://github.com/dnSpyEx/dnSpy
- AssemblyPublicizer — https://github.com/BepInEx/BepInEx.AssemblyPublicizer
- Community hub — https://nuclearoption.community/
- Modding wiki — https://nuclearoptionmods.miraheze.org/wiki/Main_Page

---

## 4. Working rules: sandbox the game, never touch Steam

All work lives in this repo. The runnable modded game lives in a gitignored sandbox that mirrors the
Steam install. The Steam copy is never modified.

`.gitignore`: `.sandbox/`, `lib/`, `**/bin/`, `**/obj/`, `*.user`.

How the sandbox is built (`scripts/setup-sandbox.ps1`):

1. Copy the small game-root files (exe, UnityPlayer.dll, crash handler) into `.sandbox/game/`.
2. **Junction** the big data folder so you don't duplicate gigabytes and never write to it:
   `cmd /c mklink /J ".sandbox\game\NuclearOption_Data" "<real game>\NuclearOption_Data"`
3. Write `.sandbox/game/steam_appid.txt` containing `2168680`.
4. Install **BepInEx 5 x64 Mono** into `.sandbox/game/` root, plus ConfigurationManager into `BepInEx/plugins`.
5. Run the publicizer on the sandboxed `Assembly-CSharp.dll`, output `Assembly-CSharp_publicized.dll`
   into `lib/`. Copy the `UnityEngine.*.dll` you need into `lib/` too. Gitignored, never committed.

`run.ps1` launches `.sandbox/game/NuclearOption.exe` (Steam must be running). The plugin DLL is
auto-copied into the sandbox by the build.

---

## 5. Finding the game (read-only)

Don't hardcode a path; Steam libraries move between drives. Locate it via
`HKCU:\Software\Valve\Steam` → `SteamPath`, parse `steamapps\libraryfolders.vdf` for the library that
lists app `2168680`; install dir is `<library>\steamapps\common\Nuclear Option`.
`Assembly-CSharp.dll` in `...\NuclearOption_Data\Managed\` is the source of truth for every class.

---

## 6. Toolchain (Phase 0)

1. Build the sandbox. Run the sandboxed exe once to generate `BepInEx/config/BepInEx.cfg`; set
   `[Chainloader] HideGameManagerObject = true` and `[Logging.Console] Enabled = true`. Confirm F1
   opens ConfigurationManager.
2. Confirm `dotnet --info`. Target **netstandard2.1** to match BepInEx 5 / the game's Mono runtime.
3. Open `Assembly-CSharp.dll` in ILSpy/dnSpyEx (or `ilspycmd`) and keep it as your reference manual.

---

## 7. Phase 1 — decompile recon (THE GATE)

Answer one question: **does an assignment/commander layer exist, or are objectives bound to specific
units — and either way, what is the runtime call that makes an objective take effect?**

Search the type and member lists for:
- Objectives/tasking: `Objective`, `Order`, `Task`, `Goal`, `Mission`, `NetworkMission`, `Waypoint`, `Route`, `Patrol`
- Assignment/commander layer: `Commander`, `AICommander`, `Director`, `BattleManager`, `StrategyAI`,
  `Brain`, `Squad`, `Group`, `Formation`, `Assign`
- Units: `Unit`, `Vehicle`, `Aircraft`, `Ground`, `Ship`, `Combatant`, `AIPilot`, `AIController`
- Sides: `Faction`, `Team`, `Side`, `Allegiance`
- Verbs: `SetObjective`, `Assign`, `MoveTo`, `SetTarget`, `Engage`, `SetDestination`

Key checks:
- Does an `Objective`/`Mission` reference a **faction/side** (free-floating objective an assigner
  consumes — case a) or a **specific unit/list** (case b)?
- Is there a loop that periodically walks objectives and assigns idle units? That's the assigner.
- Open the AI controller's `Update`/`FixedUpdate`/`Tick`: does it re-read tasking live or once at spawn?

**Oracle shortcut:** open a saved mission file from the mission editor. Whatever objective fields the
editor writes are almost certainly the runtime objective schema.

**Report back before coding** with:
1. Whether an assigner/commander layer exists (case a or b).
2. The objective representation and how to create/insert one at runtime (exact signature).
3. If case (a): the call to add an objective to a faction and confirmation the assigner picks it up.
   If case (b): how to enumerate friendly units and set a unit's task.
4. Whether the AI re-reads tasking live or once.
5. How to enumerate the player's faction units (cross-reference MKModsNO's targeting code).
6. Go / no-go: is milestone 1 "insert an objective" or "insert an objective + write the broker"?

### Reflection dump
Pair static reading with a throwaway logger: hook a stable per-frame method, grab the player's faction
units and any objective collection, dump live fields to the BepInEx console. Use the `DumpState` toggle.

---

## 8. POC spec (Phase 2) — objective-first, only after a Phase 1 go

> In a single-player mission, the player drops one "move/attack here" objective at a world position. At
> least one friendly AI unit gets assigned to it and visibly acts on it. A marker is drawn at the
> objective and a line is drawn to each assigned unit.

Build order:
1. **Place the objective.** Bind a key. On press, resolve a world position (camera/crosshair raycast to
   terrain, or reuse the game's existing target-point if exposed). Create/insert the objective.
2. **Assignment.** Case (a): let the assigner run, read back which units it picked. Case (b): run the
   thin broker — pick suitable idle friendly units near the point, set their task.
3. **Draw it.** Marker at the objective + line to each assigned unit, with state. Start with IMGUI/
   world-space lines; this doubles as the assignment debug view, so build it early.
4. **Confirm.** Log assignment and unit headings before/after; watch in-game.

Keep it to one objective, one keybind, and the draw layer.

---

## 9. Scaffold

See `src/CommanderLayer.csproj`, `src/Plugin.cs`, and `scripts/`. Fill the `// BINDING:` gaps from
Phase 1. Once Phase 1 lands, add `src/Patches/` with small, logged Harmony hooks matching the
signatures you found.

---

## 10. Risks and decisions

- **Assigner may not exist.** If objectives are bound to specific units with no broker, "AI auto-handles
  it" becomes code you write. Report before building.
- **Tasking may be read-once.** If the AI reads its objective only at spawn, trigger re-evaluation.
- **Multiplayer authority.** Orders are host-side. Keep milestone 1 single-player/host-only.
- **Version drift.** Class/field names move between updates. Pin the version (build-hash) in the README;
  re-run Phase 1 after any update that breaks the build.
- **Don't commit or redistribute game assemblies.** `lib/` and `.sandbox/` are gitignored for a reason.

## 11. Definition of done — milestone 1

In a single-player mission the player drops one "move/attack here" objective; at least one friendly AI
unit is assigned and visibly acts on it; a marker and assignment lines are drawn. Config in the F1
menu. Build deploys to the sandbox only. The Steam install is unmodified throughout. Objective types,
area circles, defend orders, networking, and any individual-unit control are follow-on milestones.
