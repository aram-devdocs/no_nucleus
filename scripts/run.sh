#!/usr/bin/env bash
# Build the mod (deploys into .sandbox via the csproj Deploy target) and launch the sandboxed game.
# Steam must be running. Never touches the Steam install.
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "[run] building + deploying plugin..."
dotnet build "$root/src/CommanderLayer.csproj" -c Release

exe="$root/.sandbox/game/NuclearOption.exe"
if [ ! -f "$exe" ]; then
  echo "[run] sandbox missing — run scripts/setup-sandbox.ps1 first" >&2
  exit 1
fi

echo "[run] launching $exe"
( cd "$root/.sandbox/game" && ./NuclearOption.exe & )
echo "[run] launched modded Nuclear Option (Steam must be running)."
