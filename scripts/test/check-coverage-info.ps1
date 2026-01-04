#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    Coverage Information Script (Non-blocking).

.DESCRIPTION
    Provides coverage insights without failing the build.

.EXAMPLE
    ./scripts/test/check-coverage-info.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

# Import common utilities
$scriptDir = $PSScriptRoot
$devLibPath = Join-Path $scriptDir '..' 'dev' 'lib' 'Common.psm1'
if (Test-Path $devLibPath) {
    Import-Module $devLibPath -Force
    $projectRoot = Get-ProjectRoot -FromPath $scriptDir
} else {
    $projectRoot = Split-Path $scriptDir -Parent | Split-Path -Parent
}

$coverageReport = Join-Path $projectRoot 'CoverageReport' 'Summary.json'

# Helper function for colored output
function Write-CoverageStatus {
    param(
        [ValidateSet('error', 'success', 'warning', 'info')]
        [string]$Status,
        [string]$Message
    )

    switch ($Status) {
        'error'   { Write-Host "X $Message" -ForegroundColor Red }
        'success' { Write-Host "[OK] $Message" -ForegroundColor Green }
        'warning' { Write-Host "[!] $Message" -ForegroundColor Yellow }
        'info'    { Write-Host "[i] $Message" -ForegroundColor Blue }
    }
}

# Check if coverage report exists
if (-not (Test-Path $coverageReport)) {
    Write-CoverageStatus 'warning' "Coverage report not found - skipping coverage analysis"
    exit 0  # Exit successfully - don't block the build
}

# Read coverage data
$json = Get-Content $coverageReport -Raw | ConvertFrom-Json
$lineCoverage = if ($json.summary.linecoverage) { [double]$json.summary.linecoverage } else { 0 }
$branchCoverage = if ($json.summary.branchcoverage) { [double]$json.summary.branchcoverage } else { 0 }
$methodCoverage = if ($json.summary.methodcoverage) { [double]$json.summary.methodcoverage } else { 0 }

Write-CoverageStatus 'info' "Coverage Report"
Write-Host "=================="
Write-Host "Line Coverage:   $lineCoverage%"
Write-Host "Branch Coverage: $branchCoverage%"
Write-Host "Method Coverage: $methodCoverage%"
Write-Host ""

# Coverage analysis
Write-CoverageStatus 'info' "Coverage Analysis:"

function Get-CoverageFeedback {
    param(
        [string]$MetricName,
        [double]$Actual
    )

    $excellentThreshold = 80
    $goodThreshold = 60

    if ($Actual -ge $excellentThreshold) {
        Write-Host "  $MetricName`: $Actual% - Excellent!" -ForegroundColor Green
    } elseif ($Actual -ge $goodThreshold) {
        Write-Host "  [OK] $MetricName`: $Actual% - Good" -ForegroundColor Green
    } elseif ($Actual -ge 40) {
        Write-Host "  [!] $MetricName`: $Actual% - Room for improvement" -ForegroundColor Yellow
    } else {
        Write-Host "  [i] $MetricName`: $Actual% - Consider adding tests" -ForegroundColor Yellow
    }
}

Get-CoverageFeedback -MetricName "Line Coverage" -Actual $lineCoverage
Get-CoverageFeedback -MetricName "Branch Coverage" -Actual $branchCoverage
Get-CoverageFeedback -MetricName "Method Coverage" -Actual $methodCoverage

Write-Host ""

# Service-specific coverage (informational)
Write-CoverageStatus 'info' "Service Coverage:"
Write-Host "================="

function Get-ServiceCoverage {
    param(
        [string]$ServiceName,
        [string]$ServicePattern,
        [object]$Json
    )

    $coverage = $null
    if ($Json.coverage -and $Json.coverage.assemblies) {
        $assembly = $Json.coverage.assemblies | Where-Object { $_.name -like "*$ServicePattern*" } | Select-Object -First 1
        if ($assembly) {
            $coverage = $assembly.coverage
        }
    }

    if (-not $coverage -or $coverage -eq 'null') {
        Write-Host "  ${ServiceName}: No data"
    } else {
        $coverageNum = [double]$coverage
        if ($coverageNum -ge 40) {
            Write-Host "  ${ServiceName}: $coverage%" -ForegroundColor Green
        } else {
            Write-Host "  ${ServiceName}: $coverage% (consider adding tests)" -ForegroundColor Yellow
        }
    }
}

Get-ServiceCoverage -ServiceName "Core Services" -ServicePattern "ConduitLLM.Core" -Json $json
Get-ServiceCoverage -ServiceName "Gateway API" -ServicePattern "ConduitLLM.Gateway" -Json $json
Get-ServiceCoverage -ServiceName "Admin API" -ServicePattern "ConduitLLM.Admin" -Json $json

Write-Host ""

# Coverage trend suggestion
if ($lineCoverage -lt 40) {
    Write-CoverageStatus 'info' "Coverage Tips:"
    Write-Host "  * Focus on testing critical business logic first"
    Write-Host "  * Consider adding unit tests for new features"
    Write-Host "  * Use 'dotnet test' locally to check coverage"
    Write-Host "  * Run './scripts/test/coverage-dashboard.ps1 run' for detailed analysis"
}

# Always exit successfully
Write-Host ""
Write-CoverageStatus 'success' "Coverage analysis complete (informational only)"
exit 0
