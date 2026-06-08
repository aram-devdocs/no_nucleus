<#
.SYNOPSIS
  Run the deterministic genome self-play and write a reviewable genepool + report to artifacts/genomes/.
  Output is for HUMAN REVIEW — evolved genomes are not auto-applied to shipped gameplay (hand-authored
  archetypes remain the default; the coarse sim's fitness is a proxy, not ground truth).

  -Seed        : RNG seed (default 1337).
  -Generations : generations to evolve (default 6).
#>
[CmdletBinding()]
param(
    [uint64]$Seed = 1337,
    [int]$Generations = 6
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$proj = Join-Path $repo 'tools\Nucleus.Evolve\Nucleus.Evolve.csproj'
$out  = Join-Path $repo 'artifacts\genomes'

Write-Host "[evolve] building tool..." -ForegroundColor Cyan
dotnet build $proj -c Release | Out-Null
if ($LASTEXITCODE -ne 0) { throw "build failed" }

Write-Host "[evolve] running self-play (seed=$Seed, generations=$Generations)..." -ForegroundColor Cyan
dotnet run --project $proj -c Release --no-build -- $Seed $Generations $out
if ($LASTEXITCODE -ne 0) { throw "evolve run failed" }

Write-Host "`n=== report ===" -ForegroundColor Yellow
Get-Content (Join-Path $out 'report.txt')
