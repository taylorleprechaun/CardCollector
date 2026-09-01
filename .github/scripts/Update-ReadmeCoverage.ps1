<#
.SYNOPSIS
    Updates the auto-generated test-count/coverage badges in README.md from fresh test output.
#>

#Requires -Version 7

param(
    [string]$RepoRoot = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
)

$ErrorActionPreference = 'Stop'

# --- C#: TRX test counts ---
$trxFiles = Get-ChildItem -Path $RepoRoot -Recurse -Filter "*.trx" -ErrorAction SilentlyContinue
if ($trxFiles.Count -eq 0) {
    throw "No .trx files found under $RepoRoot - did 'dotnet test --logger trx' run first?"
}
$csharpTotal = 0
foreach ($file in $trxFiles) {
    [xml]$trx = Get-Content $file.FullName
    $csharpTotal += [int]$trx.TestRun.ResultSummary.Counters.total
}

# --- C#: Cobertura line coverage % ---
$coberturaFiles = Get-ChildItem -Path $RepoRoot -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue
if ($coberturaFiles.Count -eq 0) {
    throw "No coverage.cobertura.xml found under $RepoRoot - did the coverlet.msbuild coverage run happen?"
}
[xml]$cobertura = Get-Content $coberturaFiles[0].FullName
$csharpCoverage = [math]::Round([double]$cobertura.coverage.'line-rate' * 100)

# --- JS: Vitest JSON test report ---
$jsReportPath = Join-Path $RepoRoot "coverage/test-report.json"
if (-not (Test-Path $jsReportPath)) {
    throw "$jsReportPath not found - did 'vitest run --reporter=json --outputFile.json=coverage/test-report.json' run?"
}
$jsReport = Get-Content $jsReportPath -Raw | ConvertFrom-Json
$jsTotal = [int]$jsReport.numTotalTests

# --- JS: coverage-summary.json ---
$jsSummaryPath = Join-Path $RepoRoot "coverage/coverage-summary.json"
if (-not (Test-Path $jsSummaryPath)) {
    throw "$jsSummaryPath not found - did 'json-summary' get added to vitest.config.js coverage.reporter?"
}
$jsSummary = Get-Content $jsSummaryPath -Raw | ConvertFrom-Json
$jsCoverage = [math]::Round([double]$jsSummary.total.lines.pct)

# --- Render replacement block (same 4-badge shape as the existing static README badges) ---
$block = @"
![C# Tests](https://img.shields.io/badge/C%23%20tests-$csharpTotal%20passing-brightgreen)
![C# Coverage](https://img.shields.io/badge/C%23%20coverage-$csharpCoverage%25-brightgreen)
![JS Tests](https://img.shields.io/badge/JS%20tests-$jsTotal%20passing-brightgreen)
![JS Coverage](https://img.shields.io/badge/JS%20coverage-$jsCoverage%25-brightgreen)
"@.Replace("`r`n", "`n")

# --- Splice into README.md between markers ---
$readmePath = Join-Path $RepoRoot "README.md"
$readmeRaw = Get-Content $readmePath -Raw
$usesCrlf = $readmeRaw -match "`r`n"
$readme = $readmeRaw -replace "`r`n", "`n"

$pattern = '(?s)(<!-- coverage:start -->\n).*?(\n<!-- coverage:end -->)'
if ($readme -notmatch $pattern) {
    throw "coverage:start/coverage:end markers not found in README.md - check they weren't accidentally removed."
}

$newReadme = [regex]::Replace($readme, $pattern, { param($m) $m.Groups[1].Value + $block + $m.Groups[2].Value })
if ($usesCrlf) {
    $newReadme = $newReadme -replace "`n", "`r`n"
}

if ($newReadme -eq $readmeRaw) {
    Write-Host "README.md coverage block unchanged."
} else {
    Set-Content -Path $readmePath -Value $newReadme -NoNewline
    Write-Host "README.md coverage block updated."
}
