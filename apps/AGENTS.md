# apps/ — BepInEx plugins (thin)

`Nucleus.Platform` is the host/loader (owns the overlay UI, the per-frame tick via a `DynamicMap.Update`
postfix, and the game-access services). `Commander`, `Build`, `Squad`, `Warfare` are mods that register an
`IMod` with the platform; each gets a bezel button + a MODS-menu ON/OFF toggle.

Apps are **composition only** — wire libs to the game, no business logic (that belongs in `libs/`). No app
references another app. Reach the game through `Nucleus.GameSdk`/`NativeAssets`/`NativeUi`, never ad-hoc
reflection or magic strings. A `dotnet build` of an app deploys into `.sandbox`; pass
`-p:Sandbox=C:\__nodeploy__` to build without deploying.
