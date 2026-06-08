# Layer discipline

References point **downward only**; the dependency graph is acyclic and enforced by
`Nucleus.Architecture.Tests`.

## The graph
- `Nucleus.Domain` — leaf (no project refs). Types under `Nucleus.Core.*`.
- `Nucleus.Squads` → Domain. `Nucleus.Production` → Domain.
- `Nucleus.Campaign` → {Domain, Squads, Production} (the only lib above both Squads and Production).
- `Nucleus.Sim` → {Domain, Campaign}.
- `Nucleus.GameSdk` → {Domain, Squads, Production, Campaign} (+ game DLLs).
- `Nucleus.Ui` → {Domain, Production, Campaign} (+ Unity/TMP). Must NOT reference Squads (expose computed
  read-model fields on Campaign views instead).
- `Nucleus.Abstractions` → {Domain, Ui, Production, Campaign}.

## Rules
- Pure libs (Domain/Squads/Production/Campaign/Sim) never reference `UnityEngine` or `Assembly-CSharp`.
- No app references another app. Apps depend on libs only and stay thin (composition, not logic).
- Game access goes through `Nucleus.GameSdk` (the codegen seam) / `NativeAssets` / `NativeUi`.
- New allow-list entries are only for genuinely new libs, never to paper over a leak.
