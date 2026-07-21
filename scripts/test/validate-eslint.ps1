#!/usr/bin/env pwsh
#Requires -Version 5.1
<#
.SYNOPSIS
    Unified ESLint validation script.

.DESCRIPTION
    Validates ESLint configurations across all TypeScript projects.

.PARAMETER Strict
    Strict mode - fails on ANY errors (CI/CD mode).

.EXAMPLE
    ./scripts/test/validate-eslint.ps1

.EXAMPLE
    ./scripts/test/validate-eslint.ps1 -Strict
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$Strict
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

# Track validation results
$script:failed = 0
$script:totalErrors = 0
$script:totalWarnings = 0

if ($Strict) {
    Write-Host "[i] Running STRICT ESLint validation (CI/CD mode)..." -ForegroundColor Blue
} else {
    Write-Host "[i] Running ESLint validation (normal mode)..." -ForegroundColor Blue
}

function Test-EsLintDirectory {
    param(
        [string]$Directory,
        [string]$Name
    )

    $dirPath = Join-Path $projectRoot $Directory

    Write-Host ""
    Write-Host "[i] Checking $Name ($Directory)..." -ForegroundColor Cyan

    $packageJson = Join-Path $dirPath 'package.json'
    if (-not (Test-Path $packageJson)) {
        Write-Host "[!] No package.json found, skipping" -ForegroundColor Yellow
        return
    }

    # Check if ESLint is configured
    $packageContent = Get-Content $packageJson -Raw
    if ($packageContent -notmatch 'eslint') {
        Write-Host "[!] No ESLint configured, skipping" -ForegroundColor Yellow
        return
    }

    # Check for conflicting config files
    $eslintrcJs = Join-Path $dirPath '.eslintrc.js'
    $eslintrcJson = Join-Path $dirPath '.eslintrc.json'
    $eslintConfigJs = Join-Path $dirPath 'eslint.config.js'

    if ((Test-Path $eslintrcJs) -and (Test-Path $eslintConfigJs)) {
        Write-Host "X ERROR: Both .eslintrc.js and eslint.config.js exist!" -ForegroundColor Red
        Write-Host "   Remove the old .eslintrc.js file"
        $script:failed = 1
        return
    }

    if ((Test-Path $eslintrcJson) -and (Test-Path $eslintConfigJs)) {
        Write-Host "X ERROR: Both .eslintrc.json and eslint.config.js exist!" -ForegroundColor Red
        Write-Host "   Remove the old .eslintrc.json file"
        $script:failed = 1
        return
    }

    # Run ESLint and capture output
    Push-Location $dirPath
    try {
        $lintOutput = & npm run lint 2>&1 | Out-String
        $lintExitCode = $LASTEXITCODE

        # Parse the output for errors and warnings
        $errorCount = 0
        $warningCount = 0

        # Extract counts from ESLint's parenthesized summary. Anchoring the
        # match here avoids treating a diagnostic column (for example 1:34)
        # as the number of errors.
        $summaryMatch = [regex]::Match(
            $lintOutput,
            '\((\d+)\s+errors?,\s+(\d+)\s+warnings?\)'
        )
        if ($summaryMatch.Success) {
            $errorCount = [int]$summaryMatch.Groups[1].Value
            $warningCount = [int]$summaryMatch.Groups[2].Value
        }

        $script:totalErrors += $errorCount
        $script:totalWarnings += $warningCount

        if ($errorCount -gt 0) {
            Write-Host "X Found $errorCount error(s)" -ForegroundColor Red

            if ($Strict) {
                $script:failed = 1
            }

            # Show the errors (limited to avoid spam)
            $errorLines = $lintOutput -split "`n" | Where-Object { $_ -match 'error' } | Select-Object -First 10
            foreach ($line in $errorLines) {
                Write-Host "   $line"
            }

            $totalErrorLines = ($lintOutput -split "`n" | Where-Object { $_ -match 'error' }).Count
            if ($totalErrorLines -gt 10) {
                Write-Host "   ... and $($totalErrorLines - 10) more errors"
            }
        } else {
            Write-Host "[OK] No errors found" -ForegroundColor Green
        }

        if ($warningCount -gt 0) {
            Write-Host "[!] Found $warningCount warning(s) (non-blocking)" -ForegroundColor Yellow
        }

        # A non-zero exit without a diagnostic summary generally means ESLint
        # itself could not run (for example, an invalid configuration).
        if ($lintExitCode -ne 0 -and $errorCount -eq 0) {
            Write-Host "X ESLint execution failed" -ForegroundColor Red
            $script:failed = 1
        }
    } finally {
        Pop-Location
    }
}

# Validate all TypeScript projects
Test-EsLintDirectory -Directory 'SDKs/Node/Gateway' -Name 'Gateway Client'
Test-EsLintDirectory -Directory 'WebAdmin' -Name 'WebAdmin'

# Print summary
Write-Host ""
Write-Host "[i] Summary:" -ForegroundColor Cyan
Write-Host "Total Errors: $($script:totalErrors)"
Write-Host "Total Warnings: $($script:totalWarnings)"

if ($script:failed -eq 0) {
    if ($Strict) {
        Write-Host "[OK] All ESLint validations passed! (No errors)" -ForegroundColor Green
        if ($script:totalWarnings -gt 0) {
            Write-Host "[!] Consider fixing the $($script:totalWarnings) warning(s) for better code quality" -ForegroundColor Yellow
        }
    } else {
        Write-Host "[OK] All ESLint configurations are valid!" -ForegroundColor Green
        if ($script:totalErrors -gt 0) {
            Write-Host "[!] Found $($script:totalErrors) error(s) - consider running with -Strict to enforce fixes" -ForegroundColor Yellow
        }
        if ($script:totalWarnings -gt 0) {
            Write-Host "[!] Found $($script:totalWarnings) warning(s) for better code quality" -ForegroundColor Yellow
        }
    }
    exit 0
} else {
    if ($Strict) {
        Write-Host "X ESLint validation FAILED!" -ForegroundColor Red
        Write-Host "X Found $($script:totalErrors) error(s) that MUST be fixed" -ForegroundColor Red
        Write-Host ""
        Write-Host "This is the same check that runs in CI/CD."
        Write-Host "Your push/build WILL FAIL if you don't fix these errors."
        Write-Host ""
        Write-Host "To fix:"
        Write-Host "1. Run './scripts/dev/fix-sdk-errors.ps1' or './scripts/dev/fix-webadmin-errors.ps1' as appropriate"
        Write-Host "2. Manually fix any remaining errors"
        Write-Host "3. Re-run this script to verify"
    } else {
        Write-Host "X ESLint validation failed!" -ForegroundColor Red
        Write-Host "[!] Fix the issues above before pushing to avoid CI/CD failures" -ForegroundColor Yellow
    }
    exit 1
}
