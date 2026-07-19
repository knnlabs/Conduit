#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    Coverage Threshold Checker.

.DESCRIPTION
    Used by CI/CD to track coverage metrics (non-blocking).

.EXAMPLE
    ./scripts/test/check-coverage-thresholds.ps1
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
$exitCode = 0
$warningMode = $true  # Set to $true to make coverage checks non-blocking

# Helper function for colored output
function Write-CoverageStatus {
    param(
        [ValidateSet('error', 'success', 'warning')]
        [string]$Status,
        [string]$Message
    )

    switch ($Status) {
        'error'   { Write-Host "X $Message" -ForegroundColor Red }
        'success' { Write-Host "[OK] $Message" -ForegroundColor Green }
        'warning' { Write-Host "[!] $Message" -ForegroundColor Yellow }
    }
}

# Check if coverage report exists
if (-not (Test-Path $coverageReport)) {
    Write-CoverageStatus 'error' "Coverage report not found at $coverageReport"
    Write-Host "Please run tests with coverage collection first."
    exit 1
}

# Read coverage data
$json = Get-Content $coverageReport -Raw -ErrorAction SilentlyContinue | ConvertFrom-Json -ErrorAction SilentlyContinue
$lineCoverage = if ($json.summary.linecoverage) { [double]$json.summary.linecoverage } else { 0 }
$branchCoverage = if ($json.summary.branchcoverage) { [double]$json.summary.branchcoverage } else { 0 }
$methodCoverage = if ($json.summary.methodcoverage) { [double]$json.summary.methodcoverage } else { 0 }

Write-Host "Coverage Threshold Check"
Write-Host "======================="
Write-Host "Line Coverage:   $lineCoverage%"
Write-Host "Branch Coverage: $branchCoverage%"
Write-Host "Method Coverage: $methodCoverage%"
Write-Host ""

# Define thresholds (these can be gradually increased)
$minLineCoverage = 40
$minBranchCoverage = 30
$minMethodCoverage = 40

# Check overall coverage
function Test-Threshold {
    param(
        [string]$MetricName,
        [double]$Actual,
        [double]$Threshold
    )

    if ($Actual -ge $Threshold) {
        Write-CoverageStatus 'success' "$MetricName`: $Actual% (>= $Threshold%)"
    } else {
        if ($script:warningMode) {
            Write-CoverageStatus 'warning' "$MetricName`: $Actual% (< $Threshold%)"
        } else {
            Write-CoverageStatus 'error' "$MetricName`: $Actual% (< $Threshold%)"
        }
        $script:exitCode = 1
    }
}

Write-Host "Threshold Check Results:"
Test-Threshold -MetricName "Line Coverage" -Actual $lineCoverage -Threshold $minLineCoverage
Test-Threshold -MetricName "Branch Coverage" -Actual $branchCoverage -Threshold $minBranchCoverage
Test-Threshold -MetricName "Method Coverage" -Actual $methodCoverage -Threshold $minMethodCoverage

Write-Host ""

# Check critical service coverage
Write-Host "Critical Service Analysis:"
Write-Host "=========================="

function Test-ServiceCoverage {
    param(
        [string]$ServiceName,
        [string]$ServicePattern,
        [double]$MinThreshold,
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
        Write-CoverageStatus 'warning' "$ServiceName`: No coverage data found"
        return
    }

    $coverageNum = [double]$coverage
    if ($coverageNum -ge $MinThreshold) {
        Write-CoverageStatus 'success' "$ServiceName`: $coverageNum% (>= $MinThreshold%)"
    } else {
        if ($script:warningMode) {
            Write-CoverageStatus 'warning' "$ServiceName`: $coverageNum% (< $MinThreshold%)"
            Write-Host "   This critical service needs more test coverage"
        } else {
            Write-CoverageStatus 'error' "$ServiceName`: $coverageNum% (< $MinThreshold%)"
            Write-Host "   This is a critical service that requires higher coverage!"
        }
        $script:exitCode = 1
    }
}

# Critical services with their minimum thresholds
Test-ServiceCoverage -ServiceName "Core Services" -ServicePattern "ConduitLLM.Core" -MinThreshold 40 -Json $json
Test-ServiceCoverage -ServiceName "Gateway API" -ServicePattern "ConduitLLM.Gateway" -MinThreshold 35 -Json $json
Test-ServiceCoverage -ServiceName "Admin API" -ServicePattern "ConduitLLM.Admin" -MinThreshold 35 -Json $json

Write-Host ""

# Final result
if ($exitCode -eq 0) {
    Write-CoverageStatus 'success' "All coverage thresholds passed!"
    Write-Host "Your changes maintain adequate test coverage."
} else {
    if ($warningMode) {
        Write-CoverageStatus 'warning' "Coverage thresholds not met (WARNING MODE - non-blocking)"
        Write-Host ""
        Write-Host "Coverage improvement suggestions:"
        Write-Host "1. Add unit tests for uncovered code"
        Write-Host "2. Focus on critical services (Core, HTTP, Admin)"
        Write-Host "3. Ensure new features include comprehensive tests"
        Write-Host "4. Run './scripts/test/coverage-dashboard.ps1 run' to see detailed coverage"
        Write-Host ""
        Write-CoverageStatus 'warning' "Build will continue despite low coverage (WARNING MODE)"
        exit 0  # Exit with success to not block builds
    } else {
        Write-CoverageStatus 'error' "Coverage thresholds not met!"
        Write-Host ""
        Write-Host "To fix this:"
        Write-Host "1. Add unit tests for uncovered code"
        Write-Host "2. Focus on critical services (Core, HTTP, Admin)"
        Write-Host "3. Ensure new features include comprehensive tests"
        Write-Host "4. Run './scripts/test/coverage-dashboard.ps1 run' to see detailed coverage"
        exit $exitCode
    }
}
