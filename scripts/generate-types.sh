#!/usr/bin/env bash
# Regenerate src/Core/Generated/GameEnums.generated.cs from the real game assembly (lib/Assembly-CSharp.dll).
# Run after any game update. A contract test asserts the generated mirror matches the assembly.
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if [ ! -f "$root/lib/Assembly-CSharp.dll" ]; then
  echo "[gen] lib/Assembly-CSharp.dll missing — run scripts/setup-sandbox.ps1 first" >&2
  exit 1
fi
( cd "$root" && dotnet run --project tools/CommanderLayer.CodeGen -c Release )
