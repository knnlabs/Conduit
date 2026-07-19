#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    Clean EF Core Migrations and rebuild everything.

.DESCRIPTION
    Stops Docker containers, cleans build artifacts, NuGet cache, and rebuilds.

.EXAMPLE
    ./scripts/migrations/clean-build-artifacts.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

# Import common utilities
$scriptDir = $PSScriptRoot
$devLibPath = Join-Path $scriptDir '..' 'dev' 'lib' 'Common.psm1'
if (Test-Path $devLibPath) {
    Import-Module $devLibPath -Force
} else {
    # Fallback if Common.psm1 not available
    function Write-Info { param($msg) Write-Host $msg -ForegroundColor Cyan }
    function Write-Success { param($msg) Write-Host $msg -ForegroundColor Green }
}

Write-Host "=== Cleaning EF Core Migrations ===" -ForegroundColor Yellow

# Stop all containers
Write-Info "Stopping Docker containers..."
docker-compose down -v

# Clean all build artifacts
Write-Info "Cleaning build artifacts..."
$projectRoot = Split-Path $scriptDir -Parent | Split-Path -Parent
Get-ChildItem -Path $projectRoot -Include 'bin', 'obj' -Directory -Recurse -ErrorAction SilentlyContinue |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

# Clean NuGet cache for local packages
Write-Info "Cleaning NuGet cache..."
dotnet nuget locals all --clear

# Clean solution
Write-Info "Running dotnet clean..."
dotnet clean

# Restore packages
Write-Info "Restoring packages..."
dotnet restore

# Build solution
Write-Info "Building solution..."
dotnet build

# Rebuild Docker images
Write-Info "Rebuilding Docker images..."
docker-compose build --no-cache

Write-Host "=== Clean complete! ===" -ForegroundColor Green
Write-Host "You can now run: docker-compose up -d"
