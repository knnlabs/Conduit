#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    Clear all blocked IPs from Redis and the database.

.DESCRIPTION
    This script clears all blocked IPs from Redis and PostgreSQL, then restarts
    services to clear in-memory blocks.

.EXAMPLE
    ./scripts/dev/clear-blocked-ips.ps1
#>

[CmdletBinding()]
param()

# Import common utilities
$scriptDir = $PSScriptRoot
Import-Module (Join-Path $scriptDir 'lib' 'Common.psm1') -Force

# Container names
$redisContainer = 'conduit-redis-1'
$postgresContainer = 'conduit-postgres-1'
$serviceContainers = @('conduit-api-1', 'conduit-admin-1', 'conduit-webadmin-1')

function Clear-RedisBlockedIps {
    Write-Host "=== Clear Blocked IPs Script ===" -ForegroundColor Cyan
    Write-Host "This will clear all blocked IPs from Redis and the database"
    Write-Host ""

    # 1. Clear Redis blocked IPs
    Write-Host "1. Clearing Redis blocked IPs..." -ForegroundColor Yellow

    Write-Host "   - Clearing blocked_ips set"
    $null = docker exec $redisContainer redis-cli DEL "conduit:blocked_ips" 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "   No blocked_ips found"
    }

    Write-Host "   - Clearing IP-specific keys"
    $ipKeys = docker exec $redisContainer redis-cli --scan --pattern "conduit:ip:*" 2>$null
    if ($ipKeys) {
        foreach ($key in $ipKeys -split "`n" | Where-Object { $_ }) {
            $key = $key.Trim()
            if ($key) {
                Write-Host "   - Deleting $key"
                $null = docker exec $redisContainer redis-cli DEL $key
            }
        }
    }

    Write-Host "   - Clearing failed login attempts"
    $loginKeys = docker exec $redisContainer redis-cli --scan --pattern "conduit:failed_login:*" 2>$null
    if ($loginKeys) {
        foreach ($key in $loginKeys -split "`n" | Where-Object { $_ }) {
            $key = $key.Trim()
            if ($key) {
                Write-Host "   - Deleting $key"
                $null = docker exec $redisContainer redis-cli DEL $key
            }
        }
    }

    Write-Host "   - Clearing rate limit keys"
    $rateLimitKeys = docker exec $redisContainer redis-cli --scan --pattern "conduit:rate_limit:*" 2>$null
    if ($rateLimitKeys) {
        foreach ($key in $rateLimitKeys -split "`n" | Where-Object { $_ }) {
            $key = $key.Trim()
            if ($key) {
                Write-Host "   - Deleting $key"
                $null = docker exec $redisContainer redis-cli DEL $key
            }
        }
    }
}

function Clear-DatabaseBlockedIps {
    Write-Host ""
    Write-Host "2. Checking database for blocked IPs table..." -ForegroundColor Yellow

    # Check if there's a blocked_ips table
    $hasTableResult = docker exec $postgresContainer psql -U conduit -d conduitdb -t -c "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'blocked_ips');" 2>$null
    $hasTable = $hasTableResult -replace '\s', ''

    if ($hasTable -eq 't') {
        Write-Host "   - Found blocked_ips table, clearing..."
        $null = docker exec $postgresContainer psql -U conduit -d conduitdb -c "DELETE FROM blocked_ips;" 2>$null
        Write-Host "   - Cleared blocked_ips table"
    }
    else {
        Write-Host "   - No blocked_ips table found in database (this is normal)"
    }
}

function Restart-Services {
    Write-Host ""
    Write-Host "3. Restarting services to clear in-memory blocks..." -ForegroundColor Yellow

    docker restart @serviceContainers

    Write-Host ""
    Write-Host "All blocked IPs have been cleared!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Note: If services are using different container names, update this script accordingly."
}

# Main execution
try {
    Clear-RedisBlockedIps
    Clear-DatabaseBlockedIps
    Restart-Services
    exit 0
}
catch {
    Write-Err "An error occurred: $_"
    exit 1
}
