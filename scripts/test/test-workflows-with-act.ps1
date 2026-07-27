#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    Test GitHub Actions workflows locally using 'act'.

.DESCRIPTION
    Install act first: https://github.com/nektos/act

.EXAMPLE
    ./scripts/test/test-workflows-with-act.ps1
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

# Check if act is installed (system or local)
$actCmd = $null
if (Get-Command 'act' -ErrorAction SilentlyContinue) {
    $actCmd = 'act'
} else {
    $localAct = Join-Path $projectRoot 'bin' 'act'
    if (Test-Path $localAct) {
        $actCmd = $localAct
        Write-Host "[i] Using local act binary: $localAct" -ForegroundColor Blue
        Write-Host ""
    }
}

if (-not $actCmd) {
    Write-Host "X 'act' is not installed" -ForegroundColor Red
    Write-Host ""
    Write-Host "Install with:"
    Write-Host "  Windows:  choco install act-cli"
    Write-Host "  macOS:    brew install act"
    Write-Host "  Linux:    curl https://raw.githubusercontent.com/nektos/act/master/install.sh | sudo bash"
    Write-Host ""
    Write-Host "The installer creates ./bin/act - you can either:"
    Write-Host "  1. Use it directly: ./bin/act"
    Write-Host "  2. Move to PATH: sudo mv ./bin/act /usr/local/bin/"
    Write-Host "  3. Run this script (it will find ./bin/act automatically)"
    exit 1
}

Write-Host ([char]0x2501 * 54) -ForegroundColor Cyan
Write-Host "Testing GitHub Actions Workflows with 'act'"
Write-Host ([char]0x2501 * 54) -ForegroundColor Cyan
Write-Host ""

Push-Location $projectRoot

try {
    # Show available workflows
    Write-Host "[i] Available workflows and jobs:" -ForegroundColor Blue
    Write-Host ""
    & $actCmd -l
    Write-Host ""

    # Ask user what to test
    Write-Host "What would you like to test?"
    Write-Host ""
    Write-Host "  1. Validate job only (fastest - builds and tests)"
    Write-Host "  2. Full CI workflow (includes Docker builds - slow)"
    Write-Host "  3. Dry run (show what would execute)"
    Write-Host "  4. List workflows and exit"
    Write-Host ""
    $choice = Read-Host "Enter choice [1-4]"

    switch ($choice) {
        '1' {
            Write-Host ""
            Write-Host "[i] Testing validate job..." -ForegroundColor Blue
            Write-Host ""
            Write-Host "Note: This will:"
            Write-Host "  - Start PostgreSQL and Redis containers"
            Write-Host "  - Build .NET solution"
            Write-Host "  - Run tests"
            Write-Host "  - Type-check WebAdmin"
            Write-Host ""
            $confirm = Read-Host "Continue? [y/N]"
            if ($confirm -eq 'y' -or $confirm -eq 'Y') {
                # Use --container-architecture linux/amd64 for compatibility
                & $actCmd push -j validate `
                    --container-architecture linux/amd64 `
                    -P ubuntu-latest=catthehacker/ubuntu:act-latest
            }
        }
        '2' {
            Write-Host ""
            Write-Host "[i] Testing full CI workflow..." -ForegroundColor Blue
            Write-Host ""
            Write-Host "[!] WARNING: This will:" -ForegroundColor Yellow
            Write-Host "  - Run all validation tests"
            Write-Host "  - Build 3 Docker images (webadmin, http, admin)"
            Write-Host "  - Take 15-30 minutes"
            Write-Host "  - Use significant disk space"
            Write-Host ""
            $confirm = Read-Host "Continue? [y/N]"
            if ($confirm -eq 'y' -or $confirm -eq 'Y') {
                & $actCmd push `
                    --container-architecture linux/amd64 `
                    -P ubuntu-latest=catthehacker/ubuntu:act-latest
            }
        }
        '3' {
            Write-Host ""
            Write-Host "[i] Dry run - showing what would execute..." -ForegroundColor Blue
            Write-Host ""
            & $actCmd push -n
        }
        '4' {
            Write-Host ""
            Write-Host "Exiting"
            exit 0
        }
        default {
            Write-Host ""
            Write-Host "X Invalid choice" -ForegroundColor Red
            exit 1
        }
    }

    Write-Host ""
    Write-Host ([char]0x2501 * 54) -ForegroundColor Cyan
    Write-Host "Done!"
    Write-Host ([char]0x2501 * 54) -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Tips:"
    Write-Host "  - Use 'act -l' to list all workflows and jobs"
    Write-Host "  - Use 'act push -j <job-name>' to test specific jobs"
    Write-Host "  - Use 'act -n' for dry run"
    Write-Host "  - Use '--secret-file .env' to provide secrets"
    Write-Host ""
} finally {
    Pop-Location
}
