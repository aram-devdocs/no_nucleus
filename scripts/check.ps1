#requires -Version 5
# Fast inner-loop gate (seconds): build the solution with warnings-as-errors, then the always-on
# headless tests (Core unit, architecture rules, the deterministic campaign sim, log-audit, installer).
# Mirrors scripts/check.sh / CI. Use scripts/audit.ps1 for the full dashboard.
# Treat any failure as the stop signal.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host '[check] build (warnings-as-errors)...'
dotnet build "$root\Nucleus.sln" -c Release /p:TreatWarningsAsErrors=true
if ($LASTEXITCODE -ne 0) { throw 'build failed' }

Write-Host '[check] unit (Core)...'
dotnet test "$root\tests\Core\Nucleus.Domain.Tests.csproj" -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw 'Core tests failed' }

Write-Host '[check] architecture rules...'
dotnet test "$root\tests\Nucleus.Architecture.Tests\Nucleus.Architecture.Tests.csproj" -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw 'architecture tests failed' }

Write-Host '[check] campaign sim (headless e2e)...'
dotnet test "$root\tests\Nucleus.Sim.Tests\Nucleus.Sim.Tests.csproj" -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw 'sim tests failed' }

Write-Host '[check] log-audit parser...'
dotnet test "$root\tests\Nucleus.LogAudit.Tests\Nucleus.LogAudit.Tests.csproj" -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw 'log-audit tests failed' }

Write-Host '[check] installer...'
dotnet test "$root\tests\Nucleus.Installer.Tests\Nucleus.Installer.Tests.csproj" -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw 'installer tests failed' }

Write-Host '[check] OK'
