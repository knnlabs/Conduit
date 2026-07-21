#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    Conduit Development Workflow Script.

.DESCRIPTION
    Provides convenient development commands for working with the WebAdmin and SDKs
    without stopping Docker containers. Handles permissions correctly.

.PARAMETER Command
    The command to execute.

.PARAMETER Arguments
    Additional arguments for the command.

.EXAMPLE
    ./scripts/dev/dev-workflow.ps1 build-webadmin

.EXAMPLE
    ./scripts/dev/dev-workflow.ps1 build-sdk gateway

.EXAMPLE
    ./scripts/dev/dev-workflow.ps1 lint-fix-webadmin
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Command,

    [Parameter(Position = 1, ValueFromRemainingArguments)]
    [string[]]$Arguments
)

$ErrorActionPreference = 'Stop'

# Import common utilities
$scriptDir = $PSScriptRoot
Import-Module (Join-Path $scriptDir 'lib' 'Common.psm1') -Force

# Configuration
$webAdminService = 'webadmin'

function Show-Usage {
    $scriptName = $MyInvocation.ScriptName
    if (-not $scriptName) { $scriptName = "dev-workflow.ps1" }

    Write-Host @"
Usage: $scriptName <command> [options]

Development Commands (Container):
  build-webadmin          - Build the WebAdmin application
  build-sdks              - Build all public SDK packages (Common, Gateway)
  build-sdk <name>        - Build specific SDK (common|gateway)
  lint-webadmin           - Run ESLint on WebAdmin
  lint-fix-webadmin       - Run ESLint with --fix on WebAdmin
  type-check-webadmin     - Run TypeScript type checking on WebAdmin
  test-webadmin           - Run WebAdmin tests
  npm-install-webadmin    - Install WebAdmin dependencies
  npm-install-sdks        - Install all SDK dependencies
  shell                   - Open bash shell in WebAdmin container
  logs                    - Show WebAdmin container logs
  restart-webadmin        - Restart WebAdmin container
  status                  - Show container status
  exec <cmd>              - Execute any command in WebAdmin container

Local Build Commands (No Container Required):
  install-local           - Install all dependencies locally (SDKs + WebAdmin)
  build-local             - Build all TypeScript projects locally
  install-and-build-local - Install and build everything locally (fresh clone)

Utility Commands:
  fix-permissions         - Fix file permissions if needed (legacy)
  clean                   - Clean node_modules and build artifacts
  help                    - Show this help message

Examples:
  $scriptName build-webadmin               # Build WebAdmin
  $scriptName build-sdk gateway            # Build Gateway SDK only
  $scriptName lint-fix-webadmin            # Fix ESLint errors in WebAdmin
  $scriptName shell                        # Open shell in WebAdmin container
  $scriptName npm-install-webadmin         # Install WebAdmin dependencies
  $scriptName exec npm install axios       # Install a package
  $scriptName exec npm run test:unit       # Run specific test suite
  $scriptName install-and-build-local      # Fresh clone? Build everything locally

Environment Variables:
  DOCKER_COMPOSE_CMD      - Docker compose command (default: docker compose)

"@
}

function Test-Containers {
    $projectRoot = Get-ProjectRoot -FromPath $scriptDir

    Push-Location $projectRoot
    try {
        $runningServices = docker compose -f docker-compose.yml -f docker-compose.dev.yml ps --services --filter "status=running" 2>&1
        if ($runningServices -notmatch $webAdminService) {
            Write-Err "WebAdmin container is not running. Start development environment first:"
            Write-Info "  ./scripts/dev.ps1"
            exit 1
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-InWebAdmin {
    param(
        [Parameter(Mandatory)]
        [string[]]$CommandArgs
    )

    $projectRoot = Get-ProjectRoot -FromPath $scriptDir

    Write-Task "Executing in WebAdmin container: $($CommandArgs -join ' ')"

    Push-Location $projectRoot
    try {
        docker compose -f docker-compose.yml -f docker-compose.dev.yml exec $webAdminService @CommandArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Command failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }
}

function Build-WebAdmin {
    Write-Info "Building WebAdmin in container's isolated .next directory..."
    Write-Warn "This production build is separate from host .next directory"
    Invoke-InWebAdmin @('sh', '-c', 'cd /app/WebAdmin && npm run build')
    Write-Info "WebAdmin build completed (in container)"
}

function Build-Sdks {
    $projectRoot = Get-ProjectRoot -FromPath $scriptDir
    Push-Location (Join-Path $projectRoot 'SDKs/Node')
    try { npm run build } finally { Pop-Location }
}

function Build-Sdk {
    param(
        [Parameter(Mandatory)]
        [string]$SdkName
    )

    $sdkPath = switch ($SdkName.ToLower()) {
        'common' { 'Common' }
        'gateway' { 'Gateway' }
        default {
            Write-Err "Invalid SDK name: $SdkName"
            Write-Info "Valid options: common, gateway"
            exit 1
        }
    }

    Write-Info "Building $SdkName SDK..."
    $projectRoot = Get-ProjectRoot -FromPath $scriptDir
    Push-Location (Join-Path $projectRoot "SDKs/Node/$sdkPath")
    try { npm run build } finally { Pop-Location }
    Write-Info "$SdkName SDK build completed"
}

function Invoke-LintWebAdmin {
    Write-Info "Running ESLint on WebAdmin..."
    Invoke-InWebAdmin @('sh', '-c', 'cd /app/WebAdmin && npm run lint')
}

function Invoke-LintFixWebAdmin {
    Write-Info "Running ESLint with --fix on WebAdmin..."
    Invoke-InWebAdmin @('sh', '-c', 'cd /app/WebAdmin && npm run lint:fix')
}

function Invoke-TypeCheckWebAdmin {
    Write-Info "Running TypeScript type checking on WebAdmin..."
    Invoke-InWebAdmin @('sh', '-c', 'cd /app/WebAdmin && npm run type-check')
}

function Invoke-TestWebAdmin {
    Write-Info "Running WebAdmin tests..."
    Invoke-InWebAdmin @('sh', '-c', 'cd /app/WebAdmin && npm run test')
}

function Install-WebAdminDeps {
    Write-Info "Installing WebAdmin dependencies..."
    Invoke-InWebAdmin @('sh', '-c', 'cd /app/WebAdmin && npm install')
}

function Install-SdksDeps {
    $projectRoot = Get-ProjectRoot -FromPath $scriptDir
    Push-Location (Join-Path $projectRoot 'SDKs/Node')
    try { npm install } finally { Pop-Location }
}

function Open-Shell {
    Write-Info "Opening bash shell in WebAdmin container..."
    $projectRoot = Get-ProjectRoot -FromPath $scriptDir
    Push-Location $projectRoot
    try {
        docker compose -f docker-compose.yml -f docker-compose.dev.yml exec $webAdminService bash
    }
    finally {
        Pop-Location
    }
}

function Show-Logs {
    $projectRoot = Get-ProjectRoot -FromPath $scriptDir
    Write-Info "Showing WebAdmin container logs..."
    Push-Location $projectRoot
    try {
        docker compose -f docker-compose.yml -f docker-compose.dev.yml logs -f $webAdminService
    }
    finally {
        Pop-Location
    }
}

function Restart-WebAdmin {
    $projectRoot = Get-ProjectRoot -FromPath $scriptDir
    Write-Info "Restarting WebAdmin container..."
    Push-Location $projectRoot
    try {
        docker compose -f docker-compose.yml -f docker-compose.dev.yml restart $webAdminService
        Write-Info "WebAdmin container restarted"
    }
    finally {
        Pop-Location
    }
}

function Show-Status {
    $projectRoot = Get-ProjectRoot -FromPath $scriptDir
    Write-Info "Container status:"
    Push-Location $projectRoot
    try {
        docker compose -f docker-compose.yml -f docker-compose.dev.yml ps
    }
    finally {
        Pop-Location
    }
}

function Repair-Permissions {
    Write-Warn "This command is legacy and should not be needed with proper user mapping"
    Write-Info "Fixing file permissions..."

    $projectRoot = Get-ProjectRoot -FromPath $scriptDir

    if (-not (Test-IsWindows)) {
        $userId = & id -u
        $groupId = & id -g

        $paths = @(
            (Join-Path $projectRoot 'WebAdmin' 'node_modules'),
            (Join-Path $projectRoot 'WebAdmin' '.next'),
            (Join-Path $projectRoot 'SDKs' 'Node' '*' 'node_modules'),
            (Join-Path $projectRoot 'SDKs' 'Node' '*' 'dist')
        )

        foreach ($path in $paths) {
            if (Test-Path $path) {
                & sudo chown -R "${userId}:${groupId}" $path 2>$null
            }
        }
    }
    else {
        Write-Warn "Permission fixing is not needed on Windows"
    }

    Write-Info "Permissions fixed (note: container .next is isolated)"
}

function Clear-BuildArtifacts {
    Write-Info "Cleaning build artifacts..."

    $projectRoot = Get-ProjectRoot -FromPath $scriptDir

    $pathsToRemove = @(
        (Join-Path $projectRoot 'WebAdmin' 'node_modules'),
        (Join-Path $projectRoot 'WebAdmin' '.next'),
        (Join-Path $projectRoot 'SDKs' 'Node' 'Common' 'node_modules'),
        (Join-Path $projectRoot 'SDKs' 'Node' 'Common' 'dist'),
        (Join-Path $projectRoot 'SDKs' 'Node' 'Admin' 'node_modules'),
        (Join-Path $projectRoot 'SDKs' 'Node' 'Admin' 'dist'),
        (Join-Path $projectRoot 'SDKs' 'Node' 'Gateway' 'node_modules'),
        (Join-Path $projectRoot 'SDKs' 'Node' 'Gateway' 'dist')
    )

    foreach ($path in $pathsToRemove) {
        if (Test-Path $path) {
            Write-Host "  Removing: $path"
            Remove-Item -Path $path -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    Write-Info "Clean completed (container .next is preserved)"
}

function Install-LocalDeps {
    Write-Info "Installing dependencies for all TypeScript projects locally..."

    $projectRoot = Get-ProjectRoot -FromPath $scriptDir

    # Install Common SDK dependencies (no dependencies on other SDKs)
    Write-Task "Installing Common SDK dependencies..."
    Push-Location (Join-Path $projectRoot 'SDKs' 'Node' 'Common')
    try { npm install } finally { Pop-Location }

    # Install Gateway SDK dependencies (depends on Common)
    Write-Task "Installing Gateway SDK dependencies..."
    Push-Location (Join-Path $projectRoot 'SDKs' 'Node' 'Gateway')
    try { npm install } finally { Pop-Location }

    # WebAdmin owns its API clients and installs independently of the SDK workspace.
    Write-Task "Installing WebAdmin dependencies..."
    Push-Location (Join-Path $projectRoot 'WebAdmin')
    try { npm install } finally { Pop-Location }

    Write-Info "All dependencies installed successfully!"
}

function Build-LocalProjects {
    Write-Info "Building all TypeScript projects locally..."

    $projectRoot = Get-ProjectRoot -FromPath $scriptDir

    # Build Common SDK first (base dependency)
    Write-Task "Building Common SDK..."
    Push-Location (Join-Path $projectRoot 'SDKs' 'Node' 'Common')
    try { npm run build } finally { Pop-Location }

    # Build Gateway SDK (depends on Common)
    Write-Task "Building Gateway SDK..."
    Push-Location (Join-Path $projectRoot 'SDKs' 'Node' 'Gateway')
    try { npm run build } finally { Pop-Location }

    # Build WebAdmin independently.
    Write-Task "Building WebAdmin..."
    Push-Location (Join-Path $projectRoot 'WebAdmin')
    try { npm run build } finally { Pop-Location }

    Write-Info "All projects built successfully!"
}

function Install-AndBuildLocal {
    Write-Info "Installing and building all TypeScript projects locally..."
    Write-Warn "This is intended for fresh clones or CI environments"

    Install-LocalDeps
    Build-LocalProjects

    $projectRoot = Get-ProjectRoot -FromPath $scriptDir
    Write-Info "Installation and build completed successfully!"
    Write-Info "The WebAdmin production build is in: $projectRoot/WebAdmin/.next (host build)"
    Write-Warn "Note: Container has its own isolated .next directory when running in Docker"
}

# Main execution
$projectRoot = Get-ProjectRoot -FromPath $scriptDir

if (-not $Command) {
    Show-Usage
    exit 1
}

switch ($Command.ToLower()) {
    'build-webadmin' {
        Test-Containers
        Build-WebAdmin
    }
    'build-sdks' {
        Test-Containers
        Build-Sdks
    }
    'build-sdk' {
        if (-not $Arguments -or $Arguments.Count -eq 0) {
            Write-Err "SDK name required"
            Write-Info "Usage: dev-workflow.ps1 build-sdk <common|gateway>"
            exit 1
        }
        Test-Containers
        Build-Sdk -SdkName $Arguments[0]
    }
    'lint-webadmin' {
        Test-Containers
        Invoke-LintWebAdmin
    }
    'lint-fix-webadmin' {
        Test-Containers
        Invoke-LintFixWebAdmin
    }
    'type-check-webadmin' {
        Test-Containers
        Invoke-TypeCheckWebAdmin
    }
    'test-webadmin' {
        Test-Containers
        Invoke-TestWebAdmin
    }
    'npm-install-webadmin' {
        Test-Containers
        Install-WebAdminDeps
    }
    'npm-install-sdks' {
        Test-Containers
        Install-SdksDeps
    }
    'shell' {
        Test-Containers
        Open-Shell
    }
    'logs' {
        Test-Containers
        Show-Logs
    }
    'restart-webadmin' {
        Restart-WebAdmin
    }
    'status' {
        Show-Status
    }
    'fix-permissions' {
        Repair-Permissions
    }
    'clean' {
        Clear-BuildArtifacts
    }
    'install-local' {
        Install-LocalDeps
    }
    'build-local' {
        Build-LocalProjects
    }
    'install-and-build-local' {
        Install-AndBuildLocal
    }
    'exec' {
        if (-not $Arguments -or $Arguments.Count -eq 0) {
            Write-Err "No command provided to exec"
            Write-Info "Usage: dev-workflow.ps1 exec <command>"
            exit 1
        }
        Test-Containers
        Invoke-InWebAdmin $Arguments
    }
    { $_ -in 'help', '--help', '-h' } {
        Show-Usage
    }
    default {
        Write-Err "Unknown command: $Command"
        Show-Usage
        exit 1
    }
}
