# libs/ — shared SDK libraries

netstandard2.1, NuGet-published, versioned in lockstep (`libs/Directory.Build.props`).

- **Pure (Unity-free, deterministic):** `Nucleus.Domain` (leaf; types under `Nucleus.Core.*`), `Nucleus.Squads`,
  `Nucleus.Production`, `Nucleus.Campaign`, `Nucleus.Sim`. No `UnityEngine`, no `Assembly-CSharp`, no wall clock,
  no nondeterministic hashing/RNG (see `.agents/rules/determinism.md`).
- **Engine/UI:** `Nucleus.GameSdk` (codegen reflection seam + native-asset cache), `Nucleus.Ui` (uGUI kit,
  `ObjectiveVisuals` color SSOT, `NativeUi` native-widget harvest). `Nucleus.Abstractions` is the `IMod` contract.

Respect the downward-only dependency graph (`.agents/rules/layer-discipline.md`); `Ui` must not reference
`Squads`. Public types get XML docs. Logic that an app needs lives here, not in the app.
