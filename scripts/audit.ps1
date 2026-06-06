#requires -Version 5
# Full audit gate → consolidated PASS/FAIL dashboard + artifacts/audit-summary.json.
# Runs the headless layers always; game-coupled layers only when lib/Assembly-CSharp.dll is present;
# log-audit only when -LogPath is supplied. Grows as harness layers land (integration, sim, coverage,
# api-snapshot). Exit code is non-zero if any step FAILs, so hooks/CI can gate on it.
[CmdletBinding()]
param([string]$LogPath = '')

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$libPresent = Test-Path (Join-Path $root 'lib\Assembly-CSharp.dll')
$results = New-Object System.Collections.Generic.List[object]
$buildOk = $false

function Add-Result($name, $status, $detail) {
  $results.Add([pscustomobject]@{ name = $name; status = $status; detail = $detail }) | Out-Null
  $line = "[$status] $name"
  if ($detail) { $line += " - $detail" }
  Write-Host $line
}

function Test-Project($name, $proj) {
  if (-not $buildOk) { Add-Result $name 'FAIL' 'blocked: build failed'; return }
  dotnet test (Join-Path $root $proj) -c Release --no-build 2>&1 | Out-Host
  if ($LASTEXITCODE -eq 0) { Add-Result $name 'PASS' '' } else { Add-Result $name 'FAIL' "exit $LASTEXITCODE" }
}

Write-Host '=== Nucleus audit ==='

# 1. Build (warnings-as-errors) — the warning gate.
dotnet build "$root\Nucleus.sln" -c Release /p:TreatWarningsAsErrors=true 2>&1 | Out-Host
if ($LASTEXITCODE -eq 0) { $buildOk = $true; Add-Result 'build' 'PASS' '0 warnings' }
else { Add-Result 'build' 'FAIL' "exit $LASTEXITCODE" }

# 2. Always-on headless layers (Unity-free; also run in cloud CI).
Test-Project 'unit-core' 'tests\Core\Nucleus.Domain.Tests.csproj'
Test-Project 'arch'      'tests\Nucleus.Architecture.Tests\Nucleus.Architecture.Tests.csproj'
Test-Project 'sim'       'tests\Nucleus.Sim.Tests\Nucleus.Sim.Tests.csproj'
Test-Project 'logaudit'  'tests\Nucleus.LogAudit.Tests\Nucleus.LogAudit.Tests.csproj'
Test-Project 'installer' 'tests\Nucleus.Installer.Tests\Nucleus.Installer.Tests.csproj'

# 3. Game-coupled layer (only when the licensed game DLL is present). Integration tests load the
#    Unity-referencing Abstractions/Ui assemblies at runtime, so they need lib/ too.
if ($libPresent) {
  Test-Project 'contract' 'tests\GameContract\Nucleus.GameContract.Tests.csproj'
  Test-Project 'integration' 'tests\Nucleus.Integration.Tests\Nucleus.Integration.Tests.csproj'
}
else {
  Add-Result 'contract' 'SKIP' 'lib/Assembly-CSharp.dll absent'
  Add-Result 'integration' 'SKIP' 'needs lib/ Unity DLLs'
}

# 4. Log-audit (only when a playtest log is supplied; tool lands in a later Phase 0 item).
if ($LogPath) {
  $logAudit = Join-Path $root 'tools\Nucleus.LogAudit\Nucleus.LogAudit.csproj'
  if (Test-Path $logAudit) {
    dotnet run --project $logAudit -c Release -- $LogPath 2>&1 | Out-Host
    if ($LASTEXITCODE -eq 0) { Add-Result 'log-audit' 'PASS' $LogPath } else { Add-Result 'log-audit' 'FAIL' $LogPath }
  } else { Add-Result 'log-audit' 'SKIP' 'Nucleus.LogAudit not built yet' }
}

# Summary + machine-readable artifact.
$fails = @($results | Where-Object { $_.status -eq 'FAIL' })
$overall = 'PASS'
if ($fails.Count -gt 0) { $overall = 'FAIL' }

$artifacts = Join-Path $root 'artifacts'
if (-not (Test-Path $artifacts)) { New-Item -ItemType Directory -Path $artifacts | Out-Null }
$summary = [pscustomobject]@{ overall = $overall; steps = $results }
$summary | ConvertTo-Json -Depth 5 | Out-File -FilePath (Join-Path $artifacts 'audit-summary.json') -Encoding utf8

Write-Host ''
Write-Host "AUDIT: $overall"
if ($overall -eq 'FAIL') { exit 1 } else { exit 0 }
