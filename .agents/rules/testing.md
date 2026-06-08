# Testing

The mod is proven without launching the game. Every change keeps the headless gate green.

## Headless suites (no game DLLs)
- `tests/Core` — pure Domain/Campaign logic.
- `tests/Nucleus.Architecture.Tests` — dependency DAG, Unity-free purity, and the design/determinism validators.
- `tests/Nucleus.Sim.Tests` — headless campaign-brain e2e; the determinism + activity canary.
- `tests/Nucleus.LogAudit.Tests`, `tests/Nucleus.Installer.Tests`.

## Game-coupled suites (only where `lib/Assembly-CSharp.dll` is present)
- `tests/GameContract` (Mono.Cecil drift check vs the real assembly), `tests/Nucleus.Integration.Tests`.

## Rules
- Logic change ⇒ a test that pins it. Prefer asserting values over not-null.
- Never weaken, skip, or delete an existing test or arch rule to go green. A canary failure is a real signal.
- Gates run the same suites everywhere: pre-commit (Core+Arch+Sim+Installer), pre-push / CI (full `check.sh`),
  `scripts/audit.ps1` (dashboard). Keep the gate scripts in parity.
- Verify with deploy disabled while iterating: `-p:Sandbox=C:\__nodeploy__`.
