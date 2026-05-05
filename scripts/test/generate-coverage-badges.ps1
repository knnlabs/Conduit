#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    Generate coverage badges for README.

.DESCRIPTION
    This script should be run after coverage reports are generated.
    It creates badge markdown and summary files.

.EXAMPLE
    ./scripts/test/generate-coverage-badges.ps1
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

$coverageDir = Join-Path $projectRoot 'CoverageReport'
$badgesDir = Join-Path $projectRoot 'docs' 'badges'

# Create badges directory if it doesn't exist
if (-not (Test-Path $badgesDir)) {
    New-Item -ItemType Directory -Path $badgesDir -Force | Out-Null
}

Write-Host "Generating coverage badges..."

$summaryFile = Join-Path $coverageDir 'Summary.json'
if (-not (Test-Path $summaryFile)) {
    Write-Host "X Coverage summary not found at $summaryFile" -ForegroundColor Red
    Write-Host "Please run tests with coverage first: dotnet test --collect:`"XPlat Code Coverage`""
    exit 1
}

# Read and parse coverage data
$json = Get-Content $summaryFile -Raw | ConvertFrom-Json
$lineCoverage = $json.summary.linecoverage
$branchCoverage = $json.summary.branchcoverage
$methodCoverage = $json.summary.methodcoverage

Write-Host "Line Coverage: $lineCoverage%"
Write-Host "Branch Coverage: $branchCoverage%"
Write-Host "Method Coverage: $methodCoverage%"

# Function to determine badge color based on percentage
function Get-BadgeColor {
    param([double]$Percentage)

    if ($Percentage -ge 80) {
        return "brightgreen"
    } elseif ($Percentage -ge 60) {
        return "yellow"
    } elseif ($Percentage -ge 40) {
        return "orange"
    } else {
        return "red"
    }
}

# Generate badge colors
$lineColor = Get-BadgeColor -Percentage ([double]$lineCoverage)
$branchColor = Get-BadgeColor -Percentage ([double]$branchCoverage)
$methodColor = Get-BadgeColor -Percentage ([double]$methodCoverage)

# Create badge markdown
$badgeMarkdown = @"
<!-- Auto-generated coverage badges -->
[![Line Coverage](https://img.shields.io/badge/Line%20Coverage-${lineCoverage}%25-${lineColor})](https://github.com/nickna/Conduit/actions)
[![Branch Coverage](https://img.shields.io/badge/Branch%20Coverage-${branchCoverage}%25-${branchColor})](https://github.com/nickna/Conduit/actions)
[![Method Coverage](https://img.shields.io/badge/Method%20Coverage-${methodCoverage}%25-${methodColor})](https://github.com/nickna/Conduit/actions)
"@

$badgesFile = Join-Path $badgesDir 'coverage-badges.md'
Set-Content -Path $badgesFile -Value $badgeMarkdown -Encoding UTF8

# Generate coverage summary for README
$summaryMarkdown = @"
## Code Coverage

| Metric | Coverage |
|--------|----------|
| **Line Coverage** | ${lineCoverage}% |
| **Branch Coverage** | ${branchCoverage}% |
| **Method Coverage** | ${methodCoverage}% |

### Coverage by Project

"@

# Add project-specific coverage
if ($json.coverage -and $json.coverage.assemblies) {
    foreach ($assembly in $json.coverage.assemblies) {
        if ($assembly.name -like '*ConduitLLM*') {
            $summaryMarkdown += "| **$($assembly.name)** | $($assembly.coverage)% |`n"
        }
    }
} else {
    $summaryMarkdown += "| Coverage details unavailable | N/A |`n"
}

$summaryFile = Join-Path $badgesDir 'coverage-summary.md'
Set-Content -Path $summaryFile -Value $summaryMarkdown -Encoding UTF8

Write-Host ""
Write-Host "[OK] Coverage badges generated:" -ForegroundColor Green
Write-Host "   - $badgesFile"
Write-Host "   - $summaryFile"
Write-Host ""
Write-Host "Add the following to your README.md:"
Write-Host ""
Write-Host $badgeMarkdown
