# tools/ — dev tooling

- `Nucleus.CodeGen` — the typed game SDK generator. A declarative manifest lists every game member the mod
  uses; the generator verifies them against the real `Assembly-CSharp.dll` and emits typed reflection
  accessors + enum mirrors + a drift contract test. Add new game members here, never as hand-written
  reflection. Re-run after a game update (`make codegen`).
- `Nucleus.Installer` — mod/mission install logic (headless-tested).
- `Nucleus.LogAudit` — turns an in-game BepInEx log into a mechanical PASS/FAIL verdict.
- `Nucleus.Evolve` — offline deterministic genome self-play over `Nucleus.Sim` (review artifact, not shipped
  into gameplay).
