#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    Reset Entity Framework Core migrations in development environment.

.DESCRIPTION
    Resets the development database and migrations. WARNING: This will DELETE ALL DATA!

.PARAMETER RemoveMigrations
    Also remove existing migrations and create a new consolidated migration.

.EXAMPLE
    ./scripts/migrations/reset-dev-migrations.ps1

.EXAMPLE
    ./scripts/migrations/reset-dev-migrations.ps1 -RemoveMigrations
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$RemoveMigrations
)

$ErrorActionPreference = 'Stop'

# Import common utilities
$scriptDir = $PSScriptRoot
$devLibPath = Join-Path $scriptDir '..' 'dev' 'lib' 'Common.psm1'
if (Test-Path $devLibPath) {
    Import-Module $devLibPath -Force
    $projectRoot = Get-ProjectRoot -FromPath $scriptDir
} else {
    # Fallback if Common.psm1 not available
    function Write-Info { param($msg) Write-Host $msg -ForegroundColor Cyan }
    function Write-Warn { param($msg) Write-Host "WARNING: $msg" -ForegroundColor Yellow }
    $projectRoot = Split-Path $scriptDir -Parent | Split-Path -Parent
}

Write-Host "==============================================" -ForegroundColor Yellow
Write-Host "EF Core Migration Reset Script (DEVELOPMENT)" -ForegroundColor Yellow
Write-Host "==============================================" -ForegroundColor Yellow
Write-Host ""
Write-Host "WARNING: This script will:" -ForegroundColor Red
Write-Host "  - Stop all Docker containers"
Write-Host "  - Delete all database volumes"
Write-Host "  - Clean all build artifacts"
Write-Host "  - Rebuild the entire solution"
Write-Host ""

$confirm = Read-Host "Are you sure you want to continue? (yes/no)"
if ($confirm -ne 'yes') {
    Write-Host "Operation cancelled."
    exit 0
}

Write-Host ""
Write-Info "Working directory: $projectRoot"
Push-Location $projectRoot

try {
    # Step 1: Stop all containers and remove volumes
    Write-Host ""
    Write-Host "Step 1: Stopping Docker containers and removing volumes..." -ForegroundColor Yellow
    $composeExitCode = Invoke-DockerCompose -WorkingDirectory $projectRoot -UseDev -Arguments @('down', '--volumes', '--remove-orphans')
    if ($composeExitCode -ne 0) { throw 'Failed to stop the development stack.' }

    # Step 2: Clean all build artifacts
    Write-Host ""
    Write-Host "Step 2: Cleaning build artifacts..." -ForegroundColor Yellow
    Get-ChildItem -Path $projectRoot -Include 'bin', 'obj' -Directory -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '(ConduitLLM\.|SDKs[/\\])' } |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

    # Step 3: Clear NuGet cache for local packages
    Write-Host ""
    Write-Host "Step 3: Clearing NuGet cache..." -ForegroundColor Yellow
    dotnet nuget locals all --clear

    # Step 4: Remove old migration files (if requested)
    Write-Host ""
    Write-Host "Step 4: Checking for migration consolidation..." -ForegroundColor Yellow

    if ($RemoveMigrations) {
        $removeMigrationConfirm = 'yes'
    } else {
        $removeMigrationConfirm = Read-Host "Do you want to remove existing migrations? (yes/no)"
    }

    if ($removeMigrationConfirm -eq 'yes') {
        Write-Host "Removing existing migrations..."
        $configurationProject = Join-Path $projectRoot 'Shared' 'ConduitLLM.Configuration'
        $migrationsPath = Join-Path $configurationProject 'Migrations'
        if (Test-Path $migrationsPath) {
            Get-ChildItem -Path $migrationsPath -File | Remove-Item -Force
        }

        Write-Host ""
        Write-Host "Creating new consolidated migration..."
        Push-Location $configurationProject
        try {
            dotnet ef migrations add InitialCreate
        } finally {
            Pop-Location
        }
    }

    # Step 5: Build solution
    Write-Host ""
    Write-Host "Step 5: Building solution..." -ForegroundColor Yellow
    dotnet build

    # Step 6: Build Docker images
    Write-Host ""
    Write-Host "Step 6: Building Docker images..." -ForegroundColor Yellow
    $composeExitCode = Invoke-DockerCompose -WorkingDirectory $projectRoot -UseDev -Arguments @('build', '--no-cache')
    if ($composeExitCode -ne 0) { throw 'Failed to rebuild the development images.' }

    # Step 7: Start services
    Write-Host ""
    Write-Host "Step 7: Starting services..." -ForegroundColor Yellow
    $composeExitCode = Invoke-DockerCompose -WorkingDirectory $projectRoot -UseDev -Arguments @('up', '-d', '--wait', '--wait-timeout', '600')
    if ($composeExitCode -ne 0) { throw 'Failed to start the development stack.' }

    # Wait for services to be healthy
    Write-Host ""
    Write-Host "Waiting for services to be healthy..."
    & (Join-Path $projectRoot 'scripts' 'setup' 'wait-for-services.ps1')
    if ($LASTEXITCODE -ne 0) {
        throw 'Services did not become healthy after the migration reset.'
    }

    # Step 8: Check migration status
    Write-Host ""
    Write-Host "Step 8: Checking migration status..." -ForegroundColor Yellow
    try {
        $healthResponse = Invoke-RestMethod -Uri 'http://localhost:5000/health/ready' -ErrorAction SilentlyContinue
        $migrationCheck = $healthResponse.checks | Where-Object { $_.name -eq 'migrations' }
        if ($migrationCheck) {
            $migrationCheck | ConvertTo-Json -Depth 5
        }
    } catch {
        Write-Warn "Could not check health endpoint: $_"
    }

    Write-Host ""
    Write-Host "==============================================" -ForegroundColor Green
    Write-Host "Migration reset complete!" -ForegroundColor Green
    Write-Host "==============================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Services running at:"
    Write-Host "  - API: http://localhost:5000"
    Write-Host "  - Admin: http://localhost:5002"
    Write-Host "  - WebAdmin: http://localhost:3000"
    Write-Host ""
    Write-Host "Check logs with: docker compose -f docker-compose.yml -f docker-compose.dev.yml logs -f"
} finally {
    Pop-Location
}
