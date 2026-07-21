#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    Comprehensive TypeScript Error Checking Script.

.DESCRIPTION
    Checks ALL TypeScript projects for lint and build errors.

.PARAMETER Json
    Output in JSON format.

.PARAMETER Fix
    Attempt auto-fixes first.

.PARAMETER Verbose
    Show detailed output during checks.

.EXAMPLE
    ./scripts/test/check-typescript.ps1

.EXAMPLE
    ./scripts/test/check-typescript.ps1 -Json

.EXAMPLE
    ./scripts/test/check-typescript.ps1 -Fix
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$Json,

    [Parameter()]
    [switch]$Fix,

    [Parameter()]
    [switch]$Verbose
)

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

# Configuration
$logFile = "typescript-errors-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"

# Global error tracking
$script:projectErrors = @{}
$script:projectWarnings = @{}
$script:projectBuildStatus = @{}
$script:totalErrors = 0
$script:totalWarnings = 0
$script:failedProjects = @()

# Helper functions
function Write-Log {
    param([string]$Message)
    if (-not $Json) {
        Write-Host $Message
    }
    Add-Content -Path $logFile -Value $Message
}

function Write-LogInfo {
    param([string]$Message)
    if (-not $Json) {
        Write-Host "[OK] $Message" -ForegroundColor Green
    }
    Add-Content -Path $logFile -Value "[INFO] $Message"
}

function Write-LogWarn {
    param([string]$Message)
    if (-not $Json) {
        Write-Host "[!] $Message" -ForegroundColor Yellow
    }
    Add-Content -Path $logFile -Value "[WARN] $Message"
}

function Write-LogError {
    param([string]$Message)
    if (-not $Json) {
        Write-Host "X $Message" -ForegroundColor Red
    }
    Add-Content -Path $logFile -Value "[ERROR] $Message"
}

function Write-LogTask {
    param([string]$Message)
    if (-not $Json) {
        Write-Host "[*] $Message" -ForegroundColor Cyan
    }
    Add-Content -Path $logFile -Value "[TASK] $Message"
}

function Write-LogSection {
    param([string]$Message)
    if (-not $Json) {
        Write-Host ""
        Write-Host ([char]0x2501 * 40) -ForegroundColor Magenta
        Write-Host "  $Message" -ForegroundColor Magenta
        Write-Host ([char]0x2501 * 40) -ForegroundColor Magenta
    }
    Add-Content -Path $logFile -Value "`n========== $Message =========="
}

function Get-ErrorCounts {
    param([string]$Output)

    $errorCount = 0
    $warningCount = 0

    # Try different patterns for counting errors
    $errorMatch = [regex]::Match($Output, '(\d+)\s+error')
    if ($errorMatch.Success) {
        $errorCount = [int]$errorMatch.Groups[1].Value
    }

    $problemMatch = [regex]::Match($Output, '\u2716\s+(\d+)\s+problem')
    if ($problemMatch.Success -and $errorCount -eq 0) {
        $errorCount = [int]$problemMatch.Groups[1].Value
    }

    # Count warnings
    $warningMatch = [regex]::Match($Output, '(\d+)\s+warning')
    if ($warningMatch.Success) {
        $warningCount = [int]$warningMatch.Groups[1].Value
    }

    return @{ Errors = $errorCount; Warnings = $warningCount }
}

function Test-WebAdmin {
    $projectName = "WebAdmin"
    Write-LogSection "Checking WebAdmin (Next.js Application)"

    $webAdminPath = Join-Path $projectRoot 'WebAdmin'
    if (-not (Test-Path $webAdminPath)) {
        Write-LogError "WebAdmin directory not found"
        $script:projectErrors[$projectName] = "Directory not found"
        $script:failedProjects += $projectName
        return
    }

    Push-Location $webAdminPath
    try {
        $lintErrors = 0
        $lintWarnings = 0
        $typeErrors = 0

        # Check for package.json
        if (-not (Test-Path 'package.json')) {
            Write-LogError "package.json not found in WebAdmin"
            $script:projectErrors[$projectName] = "package.json missing"
            return
        }

        # Install dependencies if needed
        if (-not (Test-Path 'node_modules')) {
            Write-LogTask "Installing WebAdmin dependencies..."
            $null = & npm install 2>&1
        }

        # Run ESLint
        Write-LogTask "Running ESLint on WebAdmin..."

        if ($Fix) {
            Write-LogTask "Attempting ESLint auto-fix..."
            $null = & npm run lint:fix 2>&1
        }

        $lintOutput = & npm run lint 2>&1 | Out-String
        Add-Content -Path $logFile -Value $lintOutput

        $counts = Get-ErrorCounts -Output $lintOutput
        $lintErrors = $counts.Errors
        $lintWarnings = $counts.Warnings

        if ($lintErrors -gt 0) {
            Write-LogError "WebAdmin ESLint: $lintErrors errors, $lintWarnings warnings"
            Add-Content -Path $logFile -Value "`n--- WebAdmin ESLint Errors ---"
            $lintOutput -split "`n" | Where-Object { $_ -match 'error|Error' } | Select-Object -First 50 | ForEach-Object {
                Add-Content -Path $logFile -Value $_
            }
        } else {
            Write-LogInfo "WebAdmin ESLint: No errors found"
        }

        # Run TypeScript type checking
        Write-LogTask "Running TypeScript type check on WebAdmin..."

        $typeOutput = & npm run type-check 2>&1 | Out-String
        Add-Content -Path $logFile -Value $typeOutput

        if ($typeOutput -match 'error TS') {
            $typeErrors = ($typeOutput -split "`n" | Where-Object { $_ -match 'error TS' }).Count
            Write-LogError "WebAdmin TypeScript: $typeErrors type errors"
            Add-Content -Path $logFile -Value "`n--- WebAdmin TypeScript Errors ---"
            $typeOutput -split "`n" | Where-Object { $_ -match 'error TS' } | Select-Object -First 50 | ForEach-Object {
                Add-Content -Path $logFile -Value $_
            }
        } else {
            Write-LogInfo "WebAdmin TypeScript: No type errors found"
        }

        # Note: We do NOT run build for WebAdmin in development
        Write-LogWarn "WebAdmin build check skipped (breaks development container)"

        # Store results
        $script:projectErrors[$projectName] = $lintErrors + $typeErrors
        $script:projectWarnings[$projectName] = $lintWarnings
        $script:projectBuildStatus[$projectName] = "Skipped (Dev Safety)"

        if (($lintErrors + $typeErrors) -gt 0) {
            $script:failedProjects += $projectName
        }

        $script:totalErrors += $lintErrors + $typeErrors
        $script:totalWarnings += $lintWarnings
    } finally {
        Pop-Location
    }
}

function Test-SDK {
    param(
        [string]$SdkPath,
        [string]$SdkName
    )

    Write-LogSection "Checking $SdkName SDK"

    $fullPath = Join-Path $projectRoot $SdkPath
    if (-not (Test-Path $fullPath)) {
        Write-LogError "$SdkName directory not found at $SdkPath"
        $script:projectErrors[$SdkName] = "Directory not found"
        $script:failedProjects += $SdkName
        return
    }

    Push-Location $fullPath
    try {
        $lintErrors = 0
        $lintWarnings = 0
        $buildErrors = 0

        # Check for package.json
        if (-not (Test-Path 'package.json')) {
            Write-LogError "package.json not found in $SdkName"
            $script:projectErrors[$SdkName] = "package.json missing"
            return
        }

        # Install dependencies if needed
        if (-not (Test-Path 'node_modules')) {
            Write-LogTask "Installing $SdkName dependencies..."
            $null = & npm install 2>&1
        }

        # Check if lint script exists
        $packageJson = Get-Content 'package.json' -Raw | ConvertFrom-Json
        $hasLint = $packageJson.scripts -and $packageJson.scripts.lint

        if ($hasLint) {
            Write-LogTask "Running ESLint on $SdkName..."

            if ($Fix) {
                $hasLintFix = $packageJson.scripts.'lint:fix'
                if ($hasLintFix) {
                    Write-LogTask "Attempting ESLint auto-fix..."
                    $null = & npm run 'lint:fix' 2>&1
                } else {
                    $null = & npm run lint -- --fix 2>&1
                }
            }

            $lintOutput = & npm run lint 2>&1 | Out-String
            Add-Content -Path $logFile -Value $lintOutput

            $counts = Get-ErrorCounts -Output $lintOutput
            $lintErrors = $counts.Errors
            $lintWarnings = $counts.Warnings

            if ($lintErrors -gt 0) {
                Write-LogError "$SdkName ESLint: $lintErrors errors, $lintWarnings warnings"
                Add-Content -Path $logFile -Value "`n--- $SdkName ESLint Errors ---"
                $lintOutput -split "`n" | Where-Object { $_ -match 'error|Error' } | Select-Object -First 50 | ForEach-Object {
                    Add-Content -Path $logFile -Value $_
                }
            } else {
                Write-LogInfo "$SdkName ESLint: No errors found"
            }
        } else {
            Write-LogWarn "$SdkName`: No lint script found"
        }

        # Run TypeScript build
        Write-LogTask "Building $SdkName..."

        $buildOutput = & npm run build 2>&1 | Out-String
        Add-Content -Path $logFile -Value $buildOutput

        if ($buildOutput -match 'error TS|Error:|ERROR|Failed') {
            $buildErrors = ($buildOutput -split "`n" | Where-Object { $_ -match 'error TS|Error:|ERROR' }).Count
            if ($buildErrors -eq 0) { $buildErrors = 1 }
            Write-LogError "$SdkName Build: $buildErrors errors"
            Add-Content -Path $logFile -Value "`n--- $SdkName Build Errors ---"
            $buildOutput -split "`n" | Where-Object { $_ -match 'error TS|Error:|ERROR' } | Select-Object -First 50 | ForEach-Object {
                Add-Content -Path $logFile -Value $_
            }
            $script:projectBuildStatus[$SdkName] = "Failed"
        } else {
            Write-LogInfo "$SdkName Build: Success"
            $script:projectBuildStatus[$SdkName] = "Success"
        }

        # Store results
        $script:projectErrors[$SdkName] = $lintErrors + $buildErrors
        $script:projectWarnings[$SdkName] = $lintWarnings

        if (($lintErrors + $buildErrors) -gt 0) {
            $script:failedProjects += $SdkName
        }

        $script:totalErrors += $lintErrors + $buildErrors
        $script:totalWarnings += $lintWarnings
    } finally {
        Pop-Location
    }
}

function Write-Report {
    if ($Json) {
        # Generate JSON output
        $report = @{
            timestamp = (Get-Date -Format 'o')
            totalErrors = $script:totalErrors
            totalWarnings = $script:totalWarnings
            failedProjects = $script:failedProjects
            projects = @{}
            logFile = $logFile
        }

        foreach ($project in $script:projectErrors.Keys) {
            $report.projects[$project] = @{
                errors = $script:projectErrors[$project]
                warnings = if ($script:projectWarnings[$project]) { $script:projectWarnings[$project] } else { 0 }
                buildStatus = if ($script:projectBuildStatus[$project]) { $script:projectBuildStatus[$project] } else { "Unknown" }
            }
        }

        $report | ConvertTo-Json -Depth 3
    } else {
        # Generate human-readable report
        Write-Host ""
        Write-Host ([char]0x2550 * 55) -ForegroundColor Magenta
        Write-Host "           TYPESCRIPT ERROR CHECK SUMMARY              " -ForegroundColor Magenta
        Write-Host ([char]0x2550 * 55) -ForegroundColor Magenta
        Write-Host ""

        # Project summary table
        Write-Host ("{0,-20} | {1,-10} | {2,-10} | {3,-15}" -f "Project", "Errors", "Warnings", "Build Status")
        Write-Host ("{0} | {1} | {2} | {3}" -f ("-" * 20), ("-" * 10), ("-" * 10), ("-" * 15))

        foreach ($project in @("WebAdmin", "Gateway SDK", "Common SDK")) {
            if ($script:projectErrors.ContainsKey($project)) {
                $errors = $script:projectErrors[$project]
                $warnings = if ($script:projectWarnings[$project]) { $script:projectWarnings[$project] } else { 0 }
                $buildStatus = if ($script:projectBuildStatus[$project]) { $script:projectBuildStatus[$project] } else { "N/A" }

                $errorColor = if ($errors -gt 0) { "Red" } else { "Green" }
                $warnColor = if ($warnings -gt 0) { "Yellow" } else { "Green" }
                $buildColor = if ($buildStatus -eq "Failed") { "Red" } elseif ($buildStatus -like "Skipped*") { "Yellow" } else { "Green" }

                Write-Host -NoNewline ("{0,-20} | " -f $project)
                Write-Host -NoNewline ("{0,-10} | " -f $errors) -ForegroundColor $errorColor
                Write-Host -NoNewline ("{0,-10} | " -f $warnings) -ForegroundColor $warnColor
                Write-Host ("{0,-15}" -f $buildStatus) -ForegroundColor $buildColor
            }
        }

        Write-Host ""
        Write-Host ("-" * 55)
        Write-Host "Total Errors: " -NoNewline -ForegroundColor Cyan
        Write-Host $script:totalErrors -ForegroundColor Red
        Write-Host "Total Warnings: " -NoNewline -ForegroundColor Cyan
        Write-Host $script:totalWarnings -ForegroundColor Yellow
        Write-Host ""

        if ($script:failedProjects.Count -gt 0) {
            Write-Host "Failed Projects: $($script:failedProjects -join ', ')" -ForegroundColor Red
        } else {
            Write-Host "All projects passed!" -ForegroundColor Green
        }

        Write-Host ""
        Write-Host "Detailed log saved to: " -NoNewline -ForegroundColor Cyan
        Write-Host $logFile
        Write-Host ""

        # Quick fix suggestions
        if ($script:totalErrors -gt 0) {
            Write-Host ([char]0x2550 * 55) -ForegroundColor Yellow
            Write-Host "                  QUICK FIX COMMANDS                   " -ForegroundColor Yellow
            Write-Host ([char]0x2550 * 55) -ForegroundColor Yellow
            Write-Host ""

            if ($script:projectErrors["WebAdmin"] -and $script:projectErrors["WebAdmin"] -gt 0) {
                Write-Host "WebAdmin fixes:"
                Write-Host "  ./scripts/dev/fix-webadmin-errors.ps1 -LintOnly"
                Write-Host ""
            }

            if ($script:projectErrors["Gateway SDK"] -and $script:projectErrors["Gateway SDK"] -gt 0) {
                Write-Host "SDK fixes:"
                Write-Host "  ./scripts/dev/fix-sdk-errors.ps1"
                Write-Host ""
            }

            Write-Host "To attempt auto-fixes for all projects:"
            Write-Host "  $($MyInvocation.MyCommand.Name) -Fix"
            Write-Host ""
        }
    }
}

# Main execution
# Initialize log file
Set-Content -Path $logFile -Value "TypeScript Error Check - $(Get-Date)"
Add-Content -Path $logFile -Value "========================================"

if (-not $Json) {
    Write-Host "[i] TypeScript Error Checker" -ForegroundColor Cyan
    Write-Host "Checking all TypeScript projects for errors..."
    Write-Host ""
}

# Check WebAdmin
Test-WebAdmin

# Check SDKs
Test-SDK -SdkPath "SDKs/Node/Gateway" -SdkName "Gateway SDK"
Test-SDK -SdkPath "SDKs/Node/Common" -SdkName "Common SDK"

# Generate report
Write-Report

# Exit with appropriate code
if ($script:totalErrors -gt 0) {
    exit 1
} else {
    exit 0
}
