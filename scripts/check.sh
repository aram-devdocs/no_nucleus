#!/usr/bin/env bash
# Quality gate (bash mirror of scripts/audit.ps1 / CI). Proves the mod without launching the game:
#  1. builds the whole solution with warnings-as-errors (compiler enforces accessibility vs real Assembly-CSharp),
#  2. runs the always-on headless tests: Core logic + architecture rules,
#  3. runs the game-contract tests (Mono.Cecil) when lib/Assembly-CSharp.dll is present (skips otherwise, as CI does).
# Treat any failure as the stop signal.
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "[check] building solution (warnings-as-errors)..."
dotnet build "$root/Nucleus.sln" -c Release -p:TreatWarningsAsErrors=true

echo "[check] Core logic tests..."
dotnet test "$root/tests/Core/Nucleus.Domain.Tests.csproj" -c Release --no-build

echo "[check] architecture rules..."
dotnet test "$root/tests/Nucleus.Architecture.Tests/Nucleus.Architecture.Tests.csproj" -c Release --no-build

echo "[check] campaign sim (headless e2e)..."
dotnet test "$root/tests/Nucleus.Sim.Tests/Nucleus.Sim.Tests.csproj" -c Release --no-build

echo "[check] log-audit parser..."
dotnet test "$root/tests/Nucleus.LogAudit.Tests/Nucleus.LogAudit.Tests.csproj" -c Release --no-build

echo "[check] installer..."
dotnet test "$root/tests/Nucleus.Installer.Tests/Nucleus.Installer.Tests.csproj" -c Release --no-build

if [ -f "$root/lib/Assembly-CSharp.dll" ]; then
  echo "[check] game-contract tests..."
  dotnet test "$root/tests/GameContract/Nucleus.GameContract.Tests.csproj" -c Release --no-build
  echo "[check] integration tests (host lifecycle)..."
  dotnet test "$root/tests/Nucleus.Integration.Tests/Nucleus.Integration.Tests.csproj" -c Release --no-build
else
  echo "[check] lib/Assembly-CSharp.dll absent — skipping game-coupled tests (contract + integration; mirrors CI)."
fi

echo "[check] OK"
