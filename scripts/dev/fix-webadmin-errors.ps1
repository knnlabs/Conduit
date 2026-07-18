#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    Safe WebAdmin lint and build script with permission detection and environment validation.

.DESCRIPTION
    This script safely validates the WebAdmin development environment and runs
    linting and build processes with proper error detection and guidance.

.PARAMETER LintOnly
    Run linting and fixing only (skip build).

.PARAMETER BuildOnly
    Run build only (skip linting).

.PARAMETER CheckOnly
    Check environment and permissions only.

.EXAMPLE
    ./scripts/dev/fix-webadmin-errors.ps1

.EXAMPLE
    ./scripts/dev/fix-webadmin-errors.ps1 -LintOnly

.EXAMPLE
    ./scripts/dev/fix-webadmin-errors.ps1 -BuildOnly
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$LintOnly,

    [Parameter()]
    [switch]$BuildOnly,

    [Parameter()]
    [switch]$CheckOnly
)

$ErrorActionPreference = 'Stop'

# Import common utilities
$scriptDir = $PSScriptRoot
Import-Module (Join-Path $scriptDir 'lib' 'Common.psm1') -Force

# Global state
$script:PermissionIssues = $false
$script:LintErrors = 0
$script:BuildFailed = $false
$script:EnvironmentIssues = $false

function Test-ProjectRoot {
    Write-Task "Validating project structure..."

    $projectRoot = Get-ProjectRoot -FromPath $scriptDir

    if (-not (Test-Path (Join-Path $projectRoot 'Conduit.sln'))) {
        Write-Err "This script must be run from the Conduit root directory"
        Write-Err "Current directory: $(Get-Location)"
        exit 1
    }

    if (-not (Test-Path (Join-Path $projectRoot 'WebAdmin'))) {
        Write-Err "WebAdmin directory not found"
        exit 1
    }

    if (-not (Test-Path (Join-Path $projectRoot 'WebAdmin' 'package.json'))) {
        Write-Err "WebAdmin/package.json not found"
        exit 1
    }

    Write-Success "Project structure validated"
    return $projectRoot
}

function Test-DevelopmentEnvironment {
    Write-Task "Checking development environment..."

    # Check if Docker is running
    if (-not (Test-DockerRunning)) {
        Write-Warn "Docker is not running - host-based development assumed"
        return $true
    }

    # Check if development containers are running
    $webAdminContainers = docker ps --filter "name=conduit-webadmin" --format "{{.Names}}`t{{.Image}}" 2>$null

    if ($webAdminContainers) {
        # Check if using development image
        if ($webAdminContainers -match 'node:22-alpine') {
            Write-Success "Development containers detected and running"
        }
        else {
            Write-Err "Production containers detected (not development setup)"
            Write-Err "Found: $webAdminContainers"
            Write-Err "To fix: docker compose down --volumes --remove-orphans"
            Write-Err "Then run: ./scripts/dev.ps1"
            $script:EnvironmentIssues = $true
            return $false
        }
    }
    else {
        Write-Warn "No WebAdmin containers running - host-based development assumed"
        Write-Warn "Ensure you have Node.js and npm installed for host development"
    }

    return $true
}

function Test-Permissions {
    param(
        [Parameter(Mandatory)]
        [string]$ProjectRoot
    )

    Write-Task "Checking file and folder permissions..."

    $issuesFound = $false

    # Check WebAdmin source directory permissions
    $webAdminPath = Join-Path $ProjectRoot 'WebAdmin'
    if (-not (Test-WriteAccess -Path $webAdminPath)) {
        Write-Err "Cannot write to WebAdmin directory"
        if (-not (Test-IsWindows)) {
            Write-Err "Fix with: sudo chown -R `$USER:`$USER ./WebAdmin"
        }
        $issuesFound = $true
    }

    # Check .next directory if it exists
    $nextPath = Join-Path $webAdminPath '.next'
    if (Test-Path $nextPath) {
        if (-not (Test-WriteAccess -Path $nextPath)) {
            Write-Err "Cannot write to .next folder"
            Write-Err "This will cause build failures"
            Write-Err "Fix with: ./scripts/dev.ps1 -Clean"
            $issuesFound = $true
        }
    }

    # Check node_modules directory if it exists
    $nodeModulesPath = Join-Path $webAdminPath 'node_modules'
    if (Test-Path $nodeModulesPath) {
        if (-not (Test-WriteAccess -Path $nodeModulesPath)) {
            Write-Err "Cannot write to node_modules folder"
            Write-Err "This will cause npm install failures"
            Write-Err "Fix with: ./scripts/dev.ps1 -Clean"
            $issuesFound = $true
        }
    }

    # Check for specific build artifact directories
    $buildDirs = @(
        (Join-Path $nextPath 'cache'),
        (Join-Path $nextPath 'static')
    )

    foreach ($dir in $buildDirs) {
        if ((Test-Path $dir) -and -not (Test-WriteAccess -Path $dir)) {
            Write-Err "Cannot write to build directory: $dir"
            Write-Err "Fix with: ./scripts/dev.ps1 -Clean"
            $issuesFound = $true
        }
    }

    if ($issuesFound) {
        $script:PermissionIssues = $true
        Write-Err "Permission issues detected - builds may fail"
        Write-Host ""
        Write-Err "RECOMMENDED FIXES:"
        Write-Err "1. Full environment cleanup: ./scripts/dev.ps1 -Clean"
        if (-not (Test-IsWindows)) {
            Write-Err "2. Manual fix (if above fails): sudo chown -R `$USER:`$USER ./WebAdmin"
        }
        Write-Host ""
        return $false
    }
    else {
        Write-Success "All permission checks passed"
        return $true
    }
}

function Test-NpmScript {
    param(
        [Parameter(Mandatory)]
        [string]$ScriptName,

        [Parameter(Mandatory)]
        [string]$WebAdminPath
    )

    $packageJson = Get-Content (Join-Path $WebAdminPath 'package.json') -Raw | ConvertFrom-Json
    return $null -ne $packageJson.scripts.$ScriptName
}

function Invoke-EsLint {
    param(
        [Parameter(Mandatory)]
        [string]$WebAdminPath
    )

    Write-Task "Running ESLint validation and auto-fix..."

    Push-Location $WebAdminPath
    try {
        # Step 1: Auto-fix what can be fixed
        Write-Task "Step 1: Running ESLint auto-fix..."
        if (Test-NpmScript -ScriptName 'lint:fix' -WebAdminPath $WebAdminPath) {
            $null = npm run lint:fix 2>&1
            Write-Success "ESLint auto-fix completed"
        }
        else {
            Write-Warn "No lint:fix script found, trying direct ESLint fix"
            $null = npx next lint --fix 2>&1
            Write-Success "ESLint auto-fix completed"
        }

        # Step 2: Validate linting
        Write-Task "Step 2: Running ESLint validation..."
        $lintExitCode = 0

        if (Test-NpmScript -ScriptName 'lint' -WebAdminPath $WebAdminPath) {
            $lintOutput = npm run lint 2>&1 | Out-String
            $lintExitCode = $LASTEXITCODE
        }
        else {
            $lintOutput = npx next lint 2>&1 | Out-String
            $lintExitCode = $LASTEXITCODE
        }

        # Count errors
        $errorCount = ($lintOutput -split "`n" | Where-Object { $_ -match 'error' }).Count
        $warningCount = ($lintOutput -split "`n" | Where-Object { $_ -match 'warning' }).Count

        if ($lintExitCode -eq 0) {
            Write-Success "ESLint validation passed"
            Write-Stats "Warnings: $warningCount"
        }
        else {
            Write-Err "ESLint validation failed"
            Write-Stats "Errors: $errorCount, Warnings: $warningCount"

            # Show first 10 errors for guidance
            Write-Host ""
            Write-Err "First 10 ESLint errors:"
            $lintOutput -split "`n" | Where-Object { $_ -match 'error' } | Select-Object -First 10 | ForEach-Object {
                Write-Host "  $_" -ForegroundColor Red
            }
            Write-Host ""

            $script:LintErrors = $errorCount
        }

        return $lintExitCode -eq 0
    }
    finally {
        Pop-Location
    }
}

function Invoke-TypeCheck {
    param(
        [Parameter(Mandatory)]
        [string]$WebAdminPath
    )

    Write-Task "Running TypeScript type checking..."

    Push-Location $WebAdminPath
    try {
        if (Test-NpmScript -ScriptName 'type-check' -WebAdminPath $WebAdminPath) {
            Write-Info "Using npm run type-check"
            npm run type-check 2>&1 | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Write-Success "TypeScript type checking passed"
                return $true
            }
            else {
                Write-Err "TypeScript type checking failed"
                return $false
            }
        }
        else {
            Write-Warn "No type-check script found, using tsc directly"
            npx tsc --noEmit 2>&1 | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Write-Success "TypeScript type checking passed"
                return $true
            }
            else {
                Write-Err "TypeScript type checking failed"
                return $false
            }
        }
    }
    finally {
        Pop-Location
    }
}

function Stop-WebAdminContainer {
    # Find any container running on port 3000 (WebAdmin port)
    $containerId = docker ps --format "{{.ID}}" --filter "publish=3000" 2>$null | Select-Object -First 1

    if ($containerId) {
        $containerName = docker inspect --format='{{.Name}}' $containerId 2>$null
        $containerName = $containerName -replace '^/', ''

        Write-Warn "WebAdmin development container is running: $containerName"
        Write-Task "Stopping WebAdmin container to prevent build conflicts..."

        docker stop $containerId 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Success "WebAdmin container stopped successfully"
            return $containerId
        }
        else {
            Write-Err "Failed to stop WebAdmin container"
            return $null
        }
    }
    else {
        Write-Info "No WebAdmin container running on port 3000 - safe to build"
        return ''
    }
}

function Start-WebAdminContainer {
    param(
        [Parameter(Mandatory)]
        [string]$ContainerId
    )

    if ([string]::IsNullOrEmpty($ContainerId)) {
        return
    }

    Write-Task "Restarting WebAdmin development container..."

    docker start $ContainerId 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Success "WebAdmin container restarted"

        # Wait for container to be ready
        Write-Task "Waiting for WebAdmin to be ready..."
        $maxAttempts = 30

        for ($i = 0; $i -lt $maxAttempts; $i++) {
            $logs = docker logs $ContainerId 2>&1 | Select-Object -Last 20 | Out-String
            if ($logs -match 'Ready in') {
                Write-Success "WebAdmin is ready"
                return
            }
            Start-Sleep -Seconds 1
        }

        Write-Warn "WebAdmin container started but may not be fully ready"
    }
    else {
        Write-Err "Failed to restart WebAdmin container"
        Write-Err "To restart manually: docker start $ContainerId"
    }
}

function Invoke-Build {
    param(
        [Parameter(Mandatory)]
        [string]$WebAdminPath
    )

    Write-Task "Running build process..."

    if ($script:PermissionIssues) {
        Write-Warn "Permission issues were detected earlier"
        Write-Warn "Build may fail due to permission problems"
        Write-Host ""
    }

    # Check and stop WebAdmin container if running
    $containerId = Stop-WebAdminContainer

    Push-Location $WebAdminPath
    try {
        $buildStartTime = Get-Date

        if (Test-NpmScript -ScriptName 'build' -WebAdminPath $WebAdminPath) {
            Write-Info "Using npm run build"
            npm run build 2>&1 | Out-Null

            if ($LASTEXITCODE -eq 0) {
                $buildDuration = ((Get-Date) - $buildStartTime).TotalSeconds
                Write-Success "Build completed successfully in $([math]::Round($buildDuration))s"
            }
            else {
                Write-Err "Build failed"
                $script:BuildFailed = $true
            }
        }
        else {
            Write-Err "No build script found in package.json"
            $script:BuildFailed = $true
        }
    }
    finally {
        Pop-Location

        # Restart container if it was running before
        if ($containerId) {
            Start-WebAdminContainer -ContainerId $containerId
        }
    }

    return -not $script:BuildFailed
}

function Write-Summary {
    Write-SectionHeader -Title "SUMMARY"

    if ($script:EnvironmentIssues) {
        Write-Err "Environment validation failed"
        Write-Err "Fix development environment setup first"
        return $false
    }

    if ($script:PermissionIssues) {
        Write-Err "Permission issues detected"
        Write-Err "Run: ./scripts/dev.ps1 -Clean"
    }
    else {
        Write-Success "No permission issues found"
    }

    if ($script:LintErrors -gt 0) {
        Write-Err "ESLint errors: $($script:LintErrors)"
        Write-Err "Fix manually or use: cd WebAdmin && npm run lint:fix"
    }
    else {
        Write-Success "ESLint validation passed"
    }

    if ($script:BuildFailed) {
        Write-Err "Build failed"
        if ($script:PermissionIssues) {
            Write-Err "Likely cause: Permission issues"
            Write-Err "Fix: ./scripts/dev.ps1 -Clean"
        }
    }
    else {
        Write-Success "Build completed successfully"
    }

    # Overall status
    if (-not $script:EnvironmentIssues -and -not $script:PermissionIssues -and $script:LintErrors -eq 0 -and -not $script:BuildFailed) {
        Write-Host ""
        Write-Success "All checks passed - WebAdmin is ready!"
        return $true
    }
    else {
        Write-Host ""
        Write-Err "Issues found - see summary above for fixes"
        return $false
    }
}

# Main execution
Write-SectionHeader -Title "WEBADMIN LINT AND BUILD VALIDATION"

# Always run basic checks
$projectRoot = Test-ProjectRoot
$webAdminPath = Join-Path $projectRoot 'WebAdmin'

$null = Test-DevelopmentEnvironment
$null = Test-Permissions -ProjectRoot $projectRoot

if ($CheckOnly) {
    $success = Write-Summary
    exit $(if ($success) { 0 } else { 1 })
}

# Exit early if environment issues
if ($script:EnvironmentIssues) {
    $success = Write-Summary
    exit 1
}

# Run linting unless build-only
if (-not $BuildOnly) {
    $null = Invoke-EsLint -WebAdminPath $webAdminPath
    $null = Invoke-TypeCheck -WebAdminPath $webAdminPath
}

# Run build unless lint-only
if (-not $LintOnly) {
    $null = Invoke-Build -WebAdminPath $webAdminPath
}

$success = Write-Summary
exit $(if ($success) { 0 } else { 1 })
