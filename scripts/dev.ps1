#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    Conduit Development Environment Startup Script.

.DESCRIPTION
    Simple, focused script for starting the development environment.
    Handles container management, port conflicts, and SDK building.

.PARAMETER Clean
    Delete volumes for a fresh experience.

.PARAMETER Build
    Rebuild containers (smart caching: keeps OS layers, rebuilds .NET code).

.PARAMETER Rebuild
    Full rebuild with --no-cache (slower, use when -Build fails).

.PARAMETER WebAdmin
    Rebuild WebAdmin container (fixes Next.js issues).

.PARAMETER Logs
    Show container logs.

.PARAMETER LogService
    Specific service to show logs for (api|core|admin|webadmin).

.EXAMPLE
    ./scripts/dev.ps1

.EXAMPLE
    ./scripts/dev.ps1 -Clean

.EXAMPLE
    ./scripts/dev.ps1 -WebAdmin

.EXAMPLE
    ./scripts/dev.ps1 -Logs -LogService webadmin
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$Clean,

    [Parameter()]
    [switch]$Build,

    [Parameter()]
    [Alias('NoCache')]
    [switch]$Rebuild,

    [Parameter()]
    [switch]$WebAdmin,

    [Parameter()]
    [switch]$Logs,

    [Parameter()]
    [ValidateSet('api', 'core', 'admin', 'webadmin', '')]
    [string]$LogService = ''
)

$ErrorActionPreference = 'Stop'

# Import common utilities
$scriptDir = $PSScriptRoot
Import-Module (Join-Path $scriptDir 'dev' 'lib' 'Common.psm1') -Force

# Get project root
$projectRoot = Get-ProjectRoot -FromPath $scriptDir

function Invoke-CleanupOnError {
    Write-Err "Startup failed. Cleaning up partial state..."
    Push-Location $projectRoot
    try {
        docker compose -f docker-compose.yml -f docker-compose.dev.yml down --remove-orphans 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-Warn "Automatic cleanup did not complete. Run 'docker compose -f docker-compose.yml -f docker-compose.dev.yml down --remove-orphans' manually."
        }
    }
    finally {
        Pop-Location
    }
    exit 1
}

function Clear-StaleContainers {
    Write-Info "Checking for stale Conduit containers..."

    # Get all Conduit-related containers (running or stopped)
    $conduitContainers = docker ps -a --filter "name=conduit-" --format "{{.Names}}" 2>$null

    if ($conduitContainers) {
        Write-Info "Found stale Conduit containers, cleaning up..."
        Push-Location $projectRoot
        try {
            docker compose -f docker-compose.yml -f docker-compose.dev.yml down --remove-orphans 2>$null
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to remove stale Conduit containers"
            }
            Write-Info "Stale containers removed"
        }
        finally {
            Pop-Location
        }
    }
}

function Test-PortConflicts {
    Write-Info "Checking for port conflicts..."

    Push-Location $projectRoot
    try {
        $composeJson = docker compose -f docker-compose.yml -f docker-compose.dev.yml config --format json
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to resolve Docker Compose configuration"
        }
    }
    finally {
        Pop-Location
    }

    try {
        $composeConfig = $composeJson | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        throw "Failed to parse Docker Compose configuration: $_"
    }

    # Discover published host ports from the fully merged Compose configuration so
    # new services cannot silently bypass this check.
    $ports = foreach ($serviceProperty in $composeConfig.services.PSObject.Properties) {
        $serviceName = $serviceProperty.Name
        foreach ($portMapping in @($serviceProperty.Value.ports)) {
            if ($null -ne $portMapping.published) {
                [PSCustomObject]@{
                    Port = [int]$portMapping.published
                    Name = $serviceName
                }
            }
        }
    }
    $ports = @($ports | Sort-Object Port -Unique)

    if ($ports.Count -eq 0) {
        throw "Docker Compose configuration does not publish any host ports"
    }

    $conflictsFound = $false
    $conflictingContainers = @()

    foreach ($portInfo in $ports) {
        $port = $portInfo.Port
        $name = $portInfo.Name

        if (Test-PortInUse -Port $port) {
            # Find what's using the port
            $container = docker ps --format "{{.Names}}" --filter "publish=$port" 2>$null | Select-Object -First 1

            if ($container) {
                if ($container -like 'conduit-*') {
                    Write-Warn "Port $port ($name) is used by stale Conduit container: $container"
                    # Will be cleaned up by Clear-StaleContainers
                }
                else {
                    Write-Warn "Port $port ($name) is used by container: $container"
                    $conflictingContainers += $container
                    $conflictsFound = $true
                }
            }
            else {
                # Port is used by a system process
                Write-Warn "Port $port ($name) is in use by a system process"
                $processInfo = Get-PortProcess -Port $port
                if ($processInfo) {
                    Write-Info "  Process: $($processInfo.ProcessName) (PID: $($processInfo.ProcessId))"
                }
                $conflictsFound = $true
            }
        }
    }

    # Handle non-Conduit Docker container conflicts
    if ($conflictingContainers.Count -gt 0) {
        Write-Host ""
        Write-Warn "The following Docker containers are blocking required ports:"
        foreach ($container in $conflictingContainers) {
            Write-Host "  - $container"
        }
        Write-Host ""

        $response = Read-Host "Stop these containers? [y/N]"
        if ($response -match '^[Yy]') {
            foreach ($container in $conflictingContainers) {
                Write-Info "Stopping $container..."
                docker stop $container 2>$null
            }
            Write-Info "Conflicting containers stopped"
        }
        else {
            Write-Err "Cannot proceed with port conflicts. Please resolve manually."
            exit 1
        }
    }
    elseif ($conflictsFound) {
        Write-Err "Port conflicts detected. Please resolve the system process conflicts and try again."
        exit 1
    }

    Write-Info "No port conflicts detected"
}

function Show-Usage {
    Write-Host @"
Conduit Development Environment Startup

Usage: dev.ps1 [options]

Options:
  -Clean           Delete volumes for fresh experience
  -Build           Rebuild containers (smart caching: keeps OS layers, rebuilds .NET code)
  -Rebuild         Full rebuild with --no-cache (slower, use when -Build fails)
  -WebAdmin        Rebuild WebAdmin container (fixes Next.js issues)
  -Logs            Show container logs
  -LogService      Specific service for logs (api|core|admin|webadmin)
  -Help            Show this help

Default behavior:
  - Automatically cleans up stale Conduit containers
  - Checks for port conflicts (offers to stop conflicting containers)
  - Build local Docker containers
  - Start from docker-compose.dev.yml
  - Mount WebAdmin directory for rapid development

Services available after startup:
  - WebAdmin:         http://localhost:3000
  - Gateway API:      http://localhost:5000/scalar/v1
  - Admin API:        http://localhost:5002/scalar/v1
  - Media Storage:    Cloudflare R2 (configured via .env)

Environment Variables:
  CONDUIT_S3_PUBLIC_BASE_URL - Set public URL for R2 bucket access

"@
}

function Test-Prerequisites {
    Write-Info "Checking prerequisites..."

    # Check if we're in the right directory
    if (-not (Test-Path (Join-Path $projectRoot 'Conduit.sln'))) {
        Write-Err "This script must be run from the Conduit root directory"
        exit 1
    }

    # Check if Docker is running
    if (-not (Test-DockerRunning)) {
        Write-Err "Docker is not running. Please start Docker."
        exit 1
    }

    # Check if compose files exist
    $composeFile = Join-Path $projectRoot 'docker-compose.yml'
    $composeDevFile = Join-Path $projectRoot 'docker-compose.dev.yml'

    if (-not (Test-Path $composeFile) -or -not (Test-Path $composeDevFile)) {
        Write-Err "docker-compose files not found"
        exit 1
    }

    Write-Info "Prerequisites check passed"
}

function Clear-Volumes {
    Write-Info "Cleaning volumes for fresh experience..."

    Push-Location $projectRoot
    try {
        # Stop containers
        docker compose -f docker-compose.yml -f docker-compose.dev.yml down --volumes --remove-orphans 2>$null

        # Remove all conduit volumes
        $volumes = docker volume ls --filter "name=conduit" --format "{{.Name}}" 2>$null
        if ($volumes) {
            foreach ($volume in $volumes -split "`n" | Where-Object { $_ }) {
                docker volume rm -f $volume 2>$null
            }
        }

        # Clean local build artifacts (host only - container has isolated .next)
        $pathsToClean = @(
            (Join-Path $projectRoot 'WebAdmin' '.next'),
            (Join-Path $projectRoot 'WebAdmin' 'node_modules'),
            (Join-Path $projectRoot 'SDKs' 'Node' '*' 'node_modules'),
            (Join-Path $projectRoot 'SDKs' 'Node' '*' 'dist')
        )

        foreach ($path in $pathsToClean) {
            if (Test-Path $path) {
                Remove-Item -Path $path -Recurse -Force -ErrorAction SilentlyContinue
            }
        }

        Write-Info "Volumes cleaned"
    }
    finally {
        Pop-Location
    }
}

function Build-Containers {
    param(
        [Parameter()]
        [switch]$NoCache
    )

    Write-Info "Building containers..."

    # Set user mapping for volume permissions
    $userIds = Get-DockerUserIds
    $env:DOCKER_USER_ID = $userIds.UserId
    $env:DOCKER_GROUP_ID = $userIds.GroupId

    # Generate timestamp to force .NET rebuild while keeping OS layers cached
    $cachebust = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    Write-Info "Using CACHEBUST: $cachebust (forces .NET code rebuild, keeps OS layers cached)"

    Push-Location $projectRoot
    try {
        $buildArgs = @(
            '-f', 'docker-compose.yml',
            '-f', 'docker-compose.dev.yml',
            'build',
            '--build-arg', "CACHEBUST=$cachebust"
        )

        if ($NoCache) {
            $buildArgs += '--no-cache'
        }

        $buildArgs += @('api', 'admin')

        docker compose @buildArgs

        if ($LASTEXITCODE -ne 0) {
            throw "Docker build failed"
        }

        Write-Info "Containers built (CACHEBUST: $cachebust)"
    }
    finally {
        Pop-Location
    }
}

function Build-Sdks {
    Write-Info "Building SDK packages for WebAdmin..."

    $commonDist = Join-Path $projectRoot 'SDKs' 'Node' 'Common' 'dist'
    $coreDist = Join-Path $projectRoot 'SDKs' 'Node' 'Core' 'dist'
    $adminDist = Join-Path $projectRoot 'SDKs' 'Node' 'Admin' 'dist'

    # Check if SDKs need building
    if ((Test-Path $commonDist) -and (Test-Path $coreDist) -and (Test-Path $adminDist)) {
        Write-Info "SDK packages already built, skipping..."
        return
    }

    Write-Info "SDK packages not built, building now..."

    # Build Common SDK first (dependency for others)
    $commonPath = Join-Path $projectRoot 'SDKs' 'Node' 'Common'
    if (Test-Path $commonPath) {
        Write-Info "Building Common SDK..."
        Push-Location $commonPath
        try {
            npm install
            npm run build
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to build Common SDK"
            }
        }
        finally {
            Pop-Location
        }
    }

    # Build Core SDK
    $corePath = Join-Path $projectRoot 'SDKs' 'Node' 'Core'
    if (Test-Path $corePath) {
        Write-Info "Building Core SDK..."
        Push-Location $corePath
        try {
            npm install
            npm run build
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to build Core SDK"
            }
        }
        finally {
            Pop-Location
        }
    }

    # Build Admin SDK
    $adminPath = Join-Path $projectRoot 'SDKs' 'Node' 'Admin'
    if (Test-Path $adminPath) {
        Write-Info "Building Admin SDK..."
        Push-Location $adminPath
        try {
            npm install
            npm run build
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to build Admin SDK"
            }
        }
        finally {
            Pop-Location
        }
    }

    Write-Info "SDK packages built successfully"
}

function Invoke-RebuildWebAdmin {
    Write-Info "Restarting WebAdmin container to fix Next.js issues..."

    # Ensure SDKs are built (WebAdmin depends on them)
    Build-Sdks

    Push-Location $projectRoot
    try {
        # Stop and remove WebAdmin container
        docker compose -f docker-compose.yml -f docker-compose.dev.yml stop webadmin 2>$null
        docker compose -f docker-compose.yml -f docker-compose.dev.yml rm -f webadmin 2>$null

        # Clean host's Next.js build artifacts (container has its own isolated .next)
        $nextPath = Join-Path $projectRoot 'WebAdmin' '.next'
        if (Test-Path $nextPath) {
            Remove-Item -Path $nextPath -Recurse -Force -ErrorAction SilentlyContinue
        }

        # Set user mapping
        $userIds = Get-DockerUserIds
        $env:DOCKER_USER_ID = $userIds.UserId
        $env:DOCKER_GROUP_ID = $userIds.GroupId

        # Start WebAdmin (no build needed - uses node:22-alpine with volume mounts)
        docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d webadmin

        Write-Info "WebAdmin container restarted"
        Write-Info "WebAdmin available at: http://localhost:3000"
    }
    finally {
        Pop-Location
    }
}

function Show-ContainerLogs {
    param(
        [Parameter()]
        [string]$Service
    )

    # Map "core" alias to "api"
    if ($Service -eq 'core') {
        $Service = 'api'
    }

    # Validate service name if provided
    if ($Service -and $Service -notmatch '^(api|admin|webadmin)$') {
        Write-Err "Invalid service: $Service"
        Write-Info "Valid services: api (or core), admin, webadmin"
        exit 1
    }

    Push-Location $projectRoot
    try {
        if (-not $Service) {
            Write-Info "Showing logs for all services (Ctrl+C to exit)..."
            docker compose -f docker-compose.yml -f docker-compose.dev.yml logs -f
        }
        else {
            Write-Info "Showing logs for $Service (Ctrl+C to exit)..."
            docker compose -f docker-compose.yml -f docker-compose.dev.yml logs -f $Service
        }
    }
    finally {
        Pop-Location
    }
}

function Start-Development {
    Write-Info "Starting development environment..."

    # Set user mapping for volume permissions
    $userIds = Get-DockerUserIds
    $env:DOCKER_USER_ID = $userIds.UserId
    $env:DOCKER_GROUP_ID = $userIds.GroupId

    Push-Location $projectRoot
    try {
        # Start all services and wait until services with health checks are healthy.
        docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d --wait --wait-timeout 300
        if ($LASTEXITCODE -ne 0) {
            throw "Docker Compose failed to start the development environment"
        }

        $expectedServices = @(docker compose -f docker-compose.yml -f docker-compose.dev.yml config --services)
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to determine expected Docker Compose services"
        }

        $runningServices = @(docker compose -f docker-compose.yml -f docker-compose.dev.yml ps --services --filter "status=running")
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to inspect running Docker Compose services"
        }

        $missingServices = @($expectedServices | Where-Object { $_ -notin $runningServices })
        if ($missingServices.Count -gt 0) {
            throw "Services did not reach running state: $($missingServices -join ', ')"
        }

        Write-Info "Development environment started!"
        Write-Host ""
        Write-Info "Services available at:"
        Write-Info "  WebAdmin:         http://localhost:3000"
        Write-Info "  Gateway API:      http://localhost:5000/scalar/v1"
        Write-Info "  Admin API:        http://localhost:5002/scalar/v1"
        Write-Info "  Media Storage:    Cloudflare R2"
        Write-Host ""
        Write-Info "The WebAdmin directory is mounted for rapid development."
        Write-Info "Changes to files will be reflected automatically."
    }
    finally {
        Pop-Location
    }
}

# Main execution
try {
    # Handle logs display
    if ($Logs) {
        Show-ContainerLogs -Service $LogService
        exit 0
    }

    # Change to project root
    Push-Location $projectRoot

    Test-Prerequisites

    # Handle WebAdmin-only rebuild
    if ($WebAdmin) {
        Invoke-RebuildWebAdmin
        exit 0
    }

    # Auto-cleanup stale containers before starting
    Clear-StaleContainers

    # Check for port conflicts
    Test-PortConflicts

    # Clean volumes if requested
    if ($Clean) {
        Clear-Volumes
    }

    # Build containers
    Build-Containers -NoCache:$Rebuild

    # Build SDKs (required for WebAdmin)
    Build-Sdks

    # Start development environment
    Start-Development
}
catch {
    Write-Err "Error: $_"
    Invoke-CleanupOnError
}
finally {
    Pop-Location
}
