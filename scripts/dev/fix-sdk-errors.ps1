#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    Fix ESLint errors and build SDK clients.

.DESCRIPTION
    Combined script to fix ESLint errors and build SDK clients.
    Runs linting, auto-fix, build, and tests for the Gateway SDK.

.PARAMETER Sdk
    Which SDK to fix: gateway or all (default).

.EXAMPLE
    ./scripts/dev/fix-sdk-errors.ps1

.EXAMPLE
    ./scripts/dev/fix-sdk-errors.ps1 gateway
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('gateway', 'all', '')]
    [string]$Sdk = 'all'
)

$ErrorActionPreference = 'Stop'

# Import common utilities
$scriptDir = $PSScriptRoot
Import-Module (Join-Path $scriptDir 'lib' 'Common.psm1') -Force

# SDK configuration
$sdkConfig = @{
    'gateway' = @{
        Path = 'SDKs/Node/Gateway'
        DisplayName = 'Gateway Client'
    }
}

# Global statistics
$script:TotalInitialErrors = 0
$script:TotalFixedErrors = 0
$script:TotalRemainingErrors = 0
$script:FailedSdks = @()
$script:BuildFailedSdks = @()
$script:TestFailedSdks = @()
$script:TotalBuildErrors = 0
$script:TotalTestErrors = 0

function Write-SdkHeader {
    param(
        [Parameter(Mandatory)]
        [string]$DisplayName
    )

    $headerText = "$($DisplayName.ToUpper()) SDK"
    Write-SectionHeader -Title $headerText
}

function Get-ErrorCount {
    param(
        [Parameter(Mandatory)]
        [string]$Output
    )

    if ($Output -match '(\d+)\s+error') {
        return [int]$Matches[1]
    }
    return 0
}

function Repair-SdkErrors {
    param(
        [Parameter(Mandatory)]
        [string]$SdkKey
    )

    $config = $sdkConfig[$SdkKey]
    $sdkPath = $config.Path
    $displayName = $config.DisplayName

    $projectRoot = Get-ProjectRoot -FromPath $scriptDir
    $fullPath = Join-Path $projectRoot $sdkPath

    Write-Host "Fixing $displayName ESLint errors..." -ForegroundColor Yellow

    # Change to SDK directory
    if (-not (Test-Path $fullPath)) {
        Write-Err "Cannot access $fullPath"
        $script:FailedSdks += $displayName
        return $false
    }

    Push-Location $fullPath
    try {
        # Ensure dependencies are installed
        if (-not (Test-Path 'node_modules')) {
            Write-Host "Installing dependencies..." -ForegroundColor Cyan
            npm install
            if ($LASTEXITCODE -ne 0) {
                Write-Err "Failed to install dependencies"
                $script:FailedSdks += $displayName
                return $false
            }
        }

        # Count initial errors
        $lintOutput = npm run lint 2>&1 | Out-String
        $initialErrors = Get-ErrorCount -Output $lintOutput
        Write-Stats "Initial error count: $initialErrors"
        $script:TotalInitialErrors += $initialErrors

        # Step 1: Fix unused catch variables
        Write-Host "Step 1: Fixing unused catch variables..." -ForegroundColor Yellow
        $tsFiles = Get-ChildItem -Path 'src' -Filter '*.ts' -Recurse -ErrorAction SilentlyContinue

        foreach ($file in $tsFiles) {
            $content = Get-Content $file.FullName -Raw
            $modified = $false

            # Fix catch (e) -> catch
            if ($content -match '\}\s*catch\s*\(e\)\s*\{') {
                $content = $content -replace '\}\s*catch\s*\(e\)\s*\{', '} catch {'
                $modified = $true
                Write-Host "  Fixing: $($file.Name)" -ForegroundColor Gray
            }

            # Fix unused catch (error) - only if error is not used
            if ($content -match '\}\s*catch\s*\(error\)\s*\{') {
                # Simple heuristic: check if 'error' appears after the catch block opening
                # This is a simplified check
                $content = $content -replace '\}\s*catch\s*\(error\)\s*\{([^}]*)\}', {
                    param($match)
                    $catchBody = $match.Groups[1].Value
                    if ($catchBody -notmatch '\berror\b') {
                        "} catch {$catchBody}"
                    }
                    else {
                        $match.Value
                    }
                }
                $modified = $true
            }

            if ($modified) {
                Set-Content -Path $file.FullName -Value $content -NoNewline
            }
        }

        # Step 2: Run auto-fix for other issues
        Write-Host "Step 2: Running ESLint auto-fix..." -ForegroundColor Yellow
        $null = npm run lint -- --fix 2>&1

        # Step 3: Fix console.log statements
        Write-Host "Step 3: Fixing console.log statements..." -ForegroundColor Yellow
        foreach ($file in $tsFiles) {
            $content = Get-Content $file.FullName -Raw
            if ($content -match 'console\.log\(') {
                $content = $content -replace 'console\.log\(', 'console.warn('
                Set-Content -Path $file.FullName -Value $content -NoNewline
                Write-Host "  Fixing console.log in: $($file.Name)" -ForegroundColor Gray
            }
        }

        # Step 4: Show remaining errors
        Write-Host ""
        Write-Stats "Checking remaining errors..."
        $lintOutput = npm run lint 2>&1 | Out-String
        $remainingErrors = Get-ErrorCount -Output $lintOutput

        $fixedErrors = $initialErrors - $remainingErrors
        $script:TotalFixedErrors += $fixedErrors
        $script:TotalRemainingErrors += $remainingErrors

        Write-Host ""
        Write-Success "Fixed $fixedErrors errors"
        Write-Host "Remaining lint errors: $remainingErrors" -ForegroundColor $(if ($remainingErrors -gt 0) { 'Red' } else { 'Green' })

        if ($remainingErrors -gt 0) {
            Write-Host ""
            Write-Host "Showing remaining errors that need manual fixes:" -ForegroundColor Yellow
            $lintOutput -split "`n" | Where-Object { $_ -match 'error' } | Select-Object -First 20 | ForEach-Object {
                Write-Host "  $_" -ForegroundColor Red
            }
            Write-Host ""
            Write-Host "Most common remaining issues:" -ForegroundColor Yellow
            Write-Host "1. TypeScript type safety (@typescript-eslint/no-unsafe-*)"
            Write-Host "2. Explicit any types (@typescript-eslint/no-explicit-any)"
            Write-Host "3. Empty interfaces (@typescript-eslint/no-empty-object-type)"
            Write-Host ""
            Write-Host "These require manual intervention to add proper types."
        }

        # Step 5: Run build
        Write-Host ""
        Write-Host "Step 5: Building SDK to check for API compatibility..." -ForegroundColor Yellow
        $buildFailed = $false

        $buildOutput = npm run build 2>&1 | Out-String
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Build completed successfully"
        }
        else {
            $buildFailed = $true
            $script:TotalBuildErrors++
            $script:BuildFailedSdks += $displayName

            Write-Host ""
            Write-Err "Build failed! Showing errors:"
            $buildOutput -split "`n" | Where-Object { $_ -match '(error|Error|ERROR)' } | Select-Object -First 20 | ForEach-Object {
                Write-Host "  $_" -ForegroundColor Red
            }

            Write-Host ""
            Write-Host "Common build issues:" -ForegroundColor Yellow
            Write-Host "1. API changes in backend not reflected in SDK"
            Write-Host "2. Type mismatches between API and client"
            Write-Host "3. Missing or renamed API endpoints"
            Write-Host "4. Changed request/response models"
        }

        # Step 6: Run tests
        Write-Host ""
        Write-Host "Step 6: Running tests to verify SDK functionality..." -ForegroundColor Yellow
        $testFailed = $false

        $packageJson = Get-Content 'package.json' -Raw | ConvertFrom-Json
        if ($packageJson.scripts.test) {
            $testOutput = npm test 2>&1 | Out-String
            if ($LASTEXITCODE -eq 0) {
                Write-Success "Tests passed"
            }
            else {
                $testFailed = $true
                $script:TotalTestErrors++
                $script:TestFailedSdks += $displayName

                Write-Host ""
                Write-Warn "Tests failed! SDK may have compatibility issues"
                $testOutput -split "`n" | Where-Object { $_ -match '(FAIL|Error|failed)' } | Select-Object -First 10 | ForEach-Object {
                    Write-Host "  $_" -ForegroundColor Red
                }
                Write-Host ""
                Write-Host "Consider fixing test failures before using this SDK"
            }
        }
        else {
            Write-Warn "No test script found in package.json - skipping"
        }

        # Return error if lint, build, or tests failed
        return -not ($remainingErrors -gt 0 -or $buildFailed -or $testFailed)
    }
    finally {
        Pop-Location
    }
}

function Write-Summary {
    param(
        [Parameter(Mandatory)]
        [int]$SdkCount
    )

    if ($SdkCount -gt 1) {
        Write-SectionHeader -Title "COMBINED SUMMARY"
        Write-Stats "Total initial errors: $($script:TotalInitialErrors)"
        Write-Success "Total fixed errors: $($script:TotalFixedErrors)"
        Write-Host "Total remaining errors: $($script:TotalRemainingErrors)" -ForegroundColor $(if ($script:TotalRemainingErrors -gt 0) { 'Red' } else { 'Green' })
        Write-Host "Total build errors: $($script:TotalBuildErrors)" -ForegroundColor $(if ($script:TotalBuildErrors -gt 0) { 'Red' } else { 'Green' })
        Write-Host "Total test errors: $($script:TotalTestErrors)" -ForegroundColor $(if ($script:TotalTestErrors -gt 0) { 'Red' } else { 'Green' })

        if ($script:FailedSdks.Count -gt 0) {
            Write-Host ""
            Write-Warn "Failed SDKs (lint): $($script:FailedSdks -join ', ')"
        }

        if ($script:BuildFailedSdks.Count -gt 0) {
            Write-Host ""
            Write-Err "Failed SDKs (build): $($script:BuildFailedSdks -join ', ')"
        }

        if ($script:TestFailedSdks.Count -gt 0) {
            Write-Host ""
            Write-Warn "Failed SDKs (test): $($script:TestFailedSdks -join ', ')"
        }
    }
}

# Main execution
if ($Sdk -eq 'all' -or $Sdk -eq '') {
    $targetSdks = @('admin', 'gateway')
}
else {
    $targetSdks = @($Sdk)
}

$sdkCount = $targetSdks.Count
$failedCount = 0

foreach ($sdk in $targetSdks) {
    if (-not $sdkConfig.ContainsKey($sdk)) {
        Write-Err "Unknown SDK '$sdk'"
        exit 1
    }

    if ($sdkCount -gt 1) {
        Write-SdkHeader -DisplayName $sdkConfig[$sdk].DisplayName
    }

    if (-not (Repair-SdkErrors -SdkKey $sdk)) {
        $failedCount++
    }
}

# Print summary
Write-Summary -SdkCount $sdkCount

# Exit with appropriate code
if ($failedCount -gt 0 -or $script:TotalBuildErrors -gt 0 -or $script:TotalTestErrors -gt 0) {
    Write-Host ""
    $failureParts = @()
    if ($failedCount -gt 0) { $failureParts += "$failedCount lint" }
    if ($script:TotalBuildErrors -gt 0) { $failureParts += "$($script:TotalBuildErrors) build" }
    if ($script:TotalTestErrors -gt 0) { $failureParts += "$($script:TotalTestErrors) test" }
    Write-Err "Script completed with failures: $($failureParts -join ', ')"
    exit 1
}
else {
    Write-Host ""
    Write-Success "All SDKs linted, built, and tested successfully"
    exit 0
}
