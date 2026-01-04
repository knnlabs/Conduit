#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    CI Build and Test Wrapper.

.DESCRIPTION
    Provides robust error handling and clear output for GitHub Actions.

.EXAMPLE
    ./scripts/test/ci-build-test.ps1
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

# Determine if running in terminal (for colored output)
$isInteractive = -not [Console]::IsOutputRedirected -and -not $env:CI

# Configuration
$coverageDir = Join-Path $projectRoot 'CoverageReport'
$testResultsDir = Join-Path $projectRoot 'TestResults'
$buildConfig = if ($env:BUILD_CONFIG) { $env:BUILD_CONFIG } else { 'Release' }
$coverageThresholdWarning = 40
$coverageThresholdInfo = 60

# Summary variables
$script:totalTests = 0
$script:passedTests = 0
$script:failedTests = 0
$script:skippedTests = 0
$script:buildStatus = 'success'
$script:coverageStatus = 'unknown'
$script:lineCoverage = 0
$script:branchCoverage = 0
$script:methodCoverage = 0

# Helper functions
function Write-Step {
    param([string]$Message)
    Write-Host ""
    if ($isInteractive) {
        Write-Host "==> $Message" -ForegroundColor Blue
    } else {
        Write-Host "==> $Message"
    }
}

function Write-Error {
    param([string]$Message)
    if ($isInteractive) {
        Write-Host "ERROR: $Message" -ForegroundColor Red
    } else {
        Write-Host "ERROR: $Message"
    }
}

function Write-Warning {
    param([string]$Message)
    if ($isInteractive) {
        Write-Host "WARNING: $Message" -ForegroundColor Yellow
    } else {
        Write-Host "WARNING: $Message"
    }
}

function Write-Success {
    param([string]$Message)
    if ($isInteractive) {
        Write-Host "SUCCESS: $Message" -ForegroundColor Green
    } else {
        Write-Host "SUCCESS: $Message"
    }
}

# Clean previous results
Write-Step "Cleaning previous test results"
if (Test-Path $testResultsDir) { Remove-Item $testResultsDir -Recurse -Force }
if (Test-Path $coverageDir) { Remove-Item $coverageDir -Recurse -Force }
New-Item -ItemType Directory -Path $testResultsDir -Force | Out-Null
New-Item -ItemType Directory -Path $coverageDir -Force | Out-Null

# Build
Write-Step "Building solution"
& dotnet build --configuration $buildConfig --no-incremental
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed!"
    $script:buildStatus = 'failed'
    exit 1
}
Write-Success "Build completed successfully"

# Run tests
Write-Step "Running tests with coverage"
$testExitCode = 0
& dotnet test `
    --no-build `
    --configuration $buildConfig `
    --logger "trx" `
    --logger "console;verbosity=minimal" `
    --collect:"XPlat Code Coverage" `
    --results-directory $testResultsDir `
    --settings (Join-Path $projectRoot '.runsettings') `
    -- RunConfiguration.TreatNoTestsAsError=false

$testExitCode = $LASTEXITCODE

# Generate coverage report
Write-Step "Generating coverage report"
$coverageFiles = Get-ChildItem -Path $testResultsDir -Filter 'coverage.cobertura.xml' -Recurse -ErrorAction SilentlyContinue

if ($coverageFiles) {
    try {
        & dotnet tool run reportgenerator `
            "-reports:$testResultsDir/**/coverage.cobertura.xml" `
            "-targetdir:$coverageDir" `
            "-reporttypes:JsonSummary;Badges" `
            "-verbosity:Warning" `
            "-title:Conduit Coverage Report" `
            "-tag:$($env:GITHUB_RUN_NUMBER ?? 'local')"

        if ($LASTEXITCODE -eq 0) {
            # Extract coverage metrics
            $summaryFile = Join-Path $coverageDir 'Summary.json'
            if (Test-Path $summaryFile) {
                $json = Get-Content $summaryFile -Raw | ConvertFrom-Json
                $script:lineCoverage = if ($json.summary.linecoverage) { [double]$json.summary.linecoverage } else { 0 }
                $script:branchCoverage = if ($json.summary.branchcoverage) { [double]$json.summary.branchcoverage } else { 0 }
                $script:methodCoverage = if ($json.summary.methodcoverage) { [double]$json.summary.methodcoverage } else { 0 }
                $script:coverageStatus = 'success'

                # Determine coverage level
                if ($script:lineCoverage -lt $coverageThresholdWarning) {
                    Write-Warning "Line coverage is low: $($script:lineCoverage)%"
                } elseif ($script:lineCoverage -lt $coverageThresholdInfo) {
                    Write-Host "Line coverage: $($script:lineCoverage)% (improving needed)"
                } else {
                    Write-Success "Line coverage: $($script:lineCoverage)%"
                }
            }
        }
    } catch {
        Write-Warning "Coverage report generation failed"
        $script:coverageStatus = 'failed'
    }
} else {
    Write-Warning "No coverage files found"
    $script:coverageStatus = 'none'
}

# Generate summary for GitHub Actions
if ($env:GITHUB_STEP_SUMMARY) {
    $summaryContent = @"
# Build & Test Summary

## Build
- **Status**: $(if ($script:buildStatus -eq 'success') { '[OK] Success' } else { 'X Failed' })
- **Configuration**: $buildConfig

## Tests
$(if ($script:totalTests -gt 0) {
"- **Total**: $($script:totalTests)
- **Passed**: [OK] $($script:passedTests)
- **Failed**: X $($script:failedTests)
- **Skipped**: $($script:skippedTests)"
if ($script:failedTests -gt 0) { "`n[!] **Some tests failed. Check the logs for details.**" }
} else {
"[!] No test results found"
})

## Coverage
$(if ($script:coverageStatus -eq 'success') {
"- **Line**: $($script:lineCoverage)%
- **Branch**: $($script:branchCoverage)%
- **Method**: $($script:methodCoverage)%

$(if ($script:lineCoverage -ge 80) { 'Excellent coverage!' }
elseif ($script:lineCoverage -ge 60) { 'Good coverage, room for improvement' }
elseif ($script:lineCoverage -ge 40) { 'Fair coverage, needs improvement' }
else { 'Low coverage, please add more tests' })"
} else {
"X Coverage data not available"
})

---
*Generated at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') UTC*
"@
    $summaryContent | Out-File -FilePath $env:GITHUB_STEP_SUMMARY -Append -Encoding UTF8
}

# Exit based on test results (not coverage)
if ($testExitCode -ne 0) {
    Write-Error "Tests failed!"
    exit $testExitCode
}

Write-Success "All tests passed!"
exit 0
