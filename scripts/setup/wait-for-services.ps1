#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    Wait for all Conduit services to be healthy.

.DESCRIPTION
    This script waits for all Conduit Docker services to report a healthy status.
    It checks each service in a loop with configurable timeout.

.PARAMETER MaxAttempts
    Maximum number of attempts before timing out. Default is 60.

.PARAMETER DelaySeconds
    Seconds to wait between attempts. Default is 2.

.EXAMPLE
    ./scripts/setup/wait-for-services.ps1

.EXAMPLE
    ./scripts/setup/wait-for-services.ps1 -MaxAttempts 30 -DelaySeconds 1
#>

[CmdletBinding()]
param(
    [Parameter()]
    [int]$MaxAttempts = 60,

    [Parameter()]
    [int]$DelaySeconds = 2
)

$ErrorActionPreference = 'Stop'

# Try to import common utilities if available
$commonModule = Join-Path $PSScriptRoot '..' 'dev' 'lib' 'Common.psm1'
if (Test-Path $commonModule) {
    Import-Module $commonModule -Force
}

Write-Host "Waiting for services to be healthy..." -ForegroundColor Yellow

# Service configuration
$services = @('postgres', 'redis', 'rabbitmq', 'api', 'admin', 'webadmin')

function Test-ServiceHealthy {
    param(
        [Parameter(Mandatory)]
        [string]$Service
    )

    $containerName = "conduit-$Service-1"

    # Check if container exists and is running
    $containerInfo = docker ps --format "{{.Names}}" --filter "name=$containerName" 2>$null
    if (-not $containerInfo -or $containerInfo.Trim() -ne $containerName) {
        return $false
    }

    # Check health status
    $health = docker inspect --format='{{.State.Health.Status}}' $containerName 2>$null
    if ($LASTEXITCODE -ne 0) {
        # Container might not have health check defined, consider it healthy if running
        $state = docker inspect --format='{{.State.Status}}' $containerName 2>$null
        return $state -eq 'running'
    }

    return $health.Trim() -eq 'healthy'
}

$attempt = 0

while ($attempt -lt $MaxAttempts) {
    $allHealthy = $true

    foreach ($service in $services) {
        if (-not (Test-ServiceHealthy -Service $service)) {
            $allHealthy = $false
            Write-Host "  Waiting for $service..." -ForegroundColor Gray
        }
    }

    if ($allHealthy) {
        Write-Host "All services are healthy!" -ForegroundColor Green
        exit 0
    }

    Start-Sleep -Seconds $DelaySeconds
    $attempt++
}

$totalSeconds = $MaxAttempts * $DelaySeconds
Write-Host "Error: Services did not become healthy within $totalSeconds seconds" -ForegroundColor Red
exit 1
