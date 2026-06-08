# Determinism

The campaign simulation and save/resume are **byte-identical across runs and processes**. This is the
project's flagship invariant; `Nucleus.Sim.Tests` (same-seed fingerprint + activity) is the canary.

## Rules
- No `string.GetHashCode()` and no `System.HashCode.Combine` anywhere in pure libs — both use a per-process
  random seed. Use `Nucleus.Core.Command.Fnv1a` for stable hashing and `DeterministicRng` (xorshift64*) for
  pseudo-randomness.
- No wall clock in pure libs: no `DateTime.Now`/`UtcNow`, no `Environment.TickCount`. Time is passed in
  (`WorldSnapshot.Time`).
- No `System.Random` / `UnityEngine.Random` in pure libs.
- Serialized collections must be written in a deterministic order (sort by `StringComparer.Ordinal`), never in
  `HashSet`/`Dictionary` enumeration order.
- Float math that feeds saved/replayed state must be order-stable.

## Enforcement
`Nucleus.Architecture.Tests` scans the pure-lib IL for the banned APIs. A `Sim.Tests` fingerprint regression is
a hard stop — fix the cause, never relax the test.
