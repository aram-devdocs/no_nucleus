#!/usr/bin/env bash
# Quality gate. Proves the mod without launching the game:
#  1. builds the plugin against the REAL Assembly-CSharp (compiler enforces accessibility),
#  2. runs the Core logic tests,
#  3. runs the game-contract tests (Mono.Cecil) that verify every game member we use exists with the
#     expected type/accessibility against lib/Assembly-CSharp.dll.
# Treat any failure as the stop signal.
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "[check] building plugin (against real Assembly-CSharp)..."
dotnet build "$root/src/CommanderLayer.csproj" -c Release

echo "[check] Core logic tests..."
dotnet test "$root/tests/Core/CommanderLayer.Tests.csproj" -c Release

echo "[check] game-contract tests..."
dotnet test "$root/tests/GameContract/CommanderLayer.GameContract.Tests.csproj" -c Release

echo "[check] OK"
