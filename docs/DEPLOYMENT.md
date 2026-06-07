# Deployment — CI/CD, accounts, secrets

How Nucleus ships to its four audiences and what you must set up once.

## Channels
| Audience | Channel | What ships | Account needed |
|----------|---------|------------|----------------|
| Mod **developers** | NuGet (nuget.org) | the `Nucleus.*` SDK packages + `Nucleus.Sdk` metapackage | nuget.org API key |
| **Players** | Thunderstore (r2modman) | `Nucleus-Platform` (host + shared libs) + each mod | Thunderstore team + token |
| Players | GitHub Releases / native loader / source | per-mod zips | — (GitHub) |
| Players | **Native installer** (`tools/Nucleus.Installer`) | a CLI that copies the plugin folders into their game's `BepInEx/plugins` | — |
| Players | Steam Workshop | the **Nucleus Dynamic Warfare** mission (depends on the mod stack) | Steam / Steamworks |

**Native installer.** `dotnet run --project tools/Nucleus.Installer -- install --game "<Nuclear Option folder>"`
copies every `Nucleus.*` plugin folder (from the installer's own folder, or `--source <dir>`) into
`<game>/BepInEx/plugins`. Bounded — it only ever touches `Nucleus.*` folders, never anything else; supports
`--dry-run` and `uninstall`. BepInEx 5 (x64, Mono) must already be installed (prerequisite, not installed by
this tool). Ship it alongside the built plugin folders in a GitHub Release zip.

**No game IP ships anywhere** — the SDK packages reference the game DLLs as build-time `HintPath`s
(`Private=false`); verified: a lib's nupkg contains only its own DLL. Consumers bring their own via `setup-sdk`.

## GitHub Actions
- **`ci.yml`**: on every push/PR, an ubuntu job runs the always-on headless layers (Core unit +
  architecture + campaign sim) with no game DLLs; the full `check.sh` (solution build + contract +
  integration) runs only when `lib/Assembly-CSharp.dll` is present.
- **`release.yml`**: on tag `v*`, two jobs —
  - **pack**: `dotnet pack` the SDK libs (+ `snupkg` symbols, deterministic, SourceLink) → `dotnet nuget push`
    to nuget.org. Gated on the api-snapshot being green so public-surface changes are intentional.
  - **package**: build each plugin, assemble `manifest.json` + `icon.png` + `README`, zip, publish via the
    Thunderstore CLI (`tcli`).

### Game-coupled CI (contract/integration/plugin build)
GitHub-hosted runners don't have the licensed game, so those steps skip there. To run them, register a
**self-hosted Windows runner** on a machine that already owns Nuclear Option and has run
`scripts/setup-sandbox.ps1` (which populates `lib/`). The proprietary `Assembly-CSharp.dll` stays on that
machine — it is never committed, never uploaded as an artifact, never a CI secret.

## Secrets (Settings → Secrets and variables → Actions)
| Secret | Used by | How to get it |
|--------|---------|---------------|
| `NUGET_API_KEY` | release.yml pack | nuget.org → account → API Keys → create (scope: push, glob `Nucleus.*`) |
| `TCLI_AUTH_TOKEN` | release.yml package | Thunderstore → team → Service Accounts → token |
| `STEAM_*` (optional) | Workshop publish | Steamworks; usually publish the mission manually via the in-game uploader first |

Add each: repo **Settings → Secrets and variables → Actions → New repository secret**.

## One-time account setup
1. **nuget.org** — sign in (Microsoft/GitHub), reserve the `Nucleus.*` IDs by pushing `0.1.0` once, create the
   scoped API key, add it as `NUGET_API_KEY`.
2. **Thunderstore** — create the `Nucleus` team, generate a service-account token, add as `TCLI_AUTH_TOKEN`.
   Package layout per mod: `manifest.json` (name/version/website/description/dependencies), `icon.png`
   (256×256), `README.md`, `CHANGELOG.md`, `plugins/<dll>`. `Nucleus-Platform` carries the shared libs and
   declares the `BepInExPack` dependency; each mod depends on `Nucleus-Platform` so the loader resolves the tree.
3. **Steam Workshop** — upload the `Nucleus Dynamic Warfare` mission via the game's mission uploader; the
   description links players to the Thunderstore/loader install for the required mods.

## Cutting a release
1. Bump the single SDK version in `libs/Directory.Build.props` (and plugin `Version`s) — no skew.
2. `pwsh scripts/audit.ps1` green (all layers).
3. Tag `vX.Y.Z` and push → `release.yml` packs+pushes NuGet and publishes Thunderstore.
4. Publish/refresh the Steam Workshop mission if the campaign changed.
