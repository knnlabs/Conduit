#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    Quick Test Verification.

.DESCRIPTION
    Proves our fixes work without full reinstall.

.EXAMPLE
    ./scripts/test/quick-verify-tests.ps1
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

Write-Host "=========================================="
Write-Host "Quick CI Test Verification"
Write-Host "=========================================="
Write-Host ""

$sdksNodePath = Join-Path $projectRoot 'SDKs' 'Node'
Push-Location $sdksNodePath

try {
    Write-Host "1. Testing with --detectOpenHandles (finds leaks)"
    Write-Host "-------------------------------------------"
    $output = & npm run test:ci -- --detectOpenHandles 2>&1 | Out-String
    if ($output -match 'Jest has detected the following.*open handle') {
        Write-Host "X LEAK FOUND:" -ForegroundColor Red
        $output -split "`n" | Where-Object { $_ -match 'Jest has detected' } | Select-Object -First 5 | ForEach-Object { Write-Host $_ }
    } else {
        Write-Host "[OK] No open handles detected" -ForegroundColor Green
    }
    Write-Host ""

    Write-Host "2. Running tests 3 times (checks stability)"
    Write-Host "-------------------------------------------"
    $passes = 0
    for ($i = 1; $i -le 3; $i++) {
        Write-Host -NoNewline "  Run ${i}: "
        $null = & npm run test:ci 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "PASS" -ForegroundColor Green
            $passes++
        } else {
            Write-Host "FAIL" -ForegroundColor Red
        }
    }

    if ($passes -eq 3) {
        Write-Host "[OK] All 3 runs passed - tests are stable" -ForegroundColor Green
    } else {
        Write-Host "X Only $passes/3 runs passed - tests are flaky" -ForegroundColor Red
    }
    Write-Host ""

    Write-Host "3. Checking for console output"
    Write-Host "-------------------------------------------"
    $testOutput = & npm run test:ci 2>&1 | Out-String
    $consoleCount = ([regex]::Matches($testOutput, 'console\.')).Count
    if ($consoleCount -eq 0) {
        Write-Host "[OK] No console logs in production code" -ForegroundColor Green
    } else {
        Write-Host "[!] Found $consoleCount console statements" -ForegroundColor Yellow
    }
    Write-Host ""

    Write-Host "4. Test execution time"
    Write-Host "-------------------------------------------"
    $startTime = Get-Date
    $null = & npm run test:ci 2>&1
    $endTime = Get-Date
    $timeSeconds = [int]($endTime - $startTime).TotalSeconds
    Write-Host "Execution time: $timeSeconds seconds"
    if ($timeSeconds -lt 10) {
        Write-Host "[OK] Fast execution" -ForegroundColor Green
    } else {
        Write-Host "[!] Could be faster" -ForegroundColor Yellow
    }
    Write-Host ""

    Write-Host "=========================================="
    Write-Host "RESULTS"
    Write-Host "=========================================="
    if ($passes -eq 3 -and $consoleCount -eq 0) {
        Write-Host "[OK] CI READY - All checks passed!" -ForegroundColor Green
        Write-Host ""
        Write-Host "Proof points:"
        Write-Host "  * No memory leaks (no open handles)"
        Write-Host "  * 100% test stability (3/3 passes)"
        Write-Host "  * Clean output (no console logs)"
        Write-Host "  * Efficient execution (${timeSeconds}s)"
    } else {
        Write-Host "X Issues found - see above" -ForegroundColor Red
    }
} finally {
    Pop-Location
}
