# tests/ — the gate

Headless (no game DLLs): `Core` (pure logic), `Nucleus.Architecture.Tests` (dependency DAG + Unity-free purity
+ design/determinism validators), `Nucleus.Sim.Tests` (campaign-brain e2e — the determinism + activity canary),
`Nucleus.LogAudit.Tests`, `Nucleus.Installer.Tests`. Game-coupled (only with `lib/Assembly-CSharp.dll`):
`GameContract` (Cecil drift vs the real assembly), `Nucleus.Integration.Tests` (host lifecycle).

Never weaken, skip, or delete a test or arch rule to go green — a canary failure is a real signal
(`.agents/rules/testing.md`). New behavior gets a test that pins the value. `Nucleus.Architecture.Tests` is
also where new design/determinism validators live.
