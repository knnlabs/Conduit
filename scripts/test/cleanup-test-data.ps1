#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    Cleanup Test Data Script for Conduit Integration Tests.

.DESCRIPTION
    This script removes all test data from the database before running tests.

.EXAMPLE
    ./scripts/test/cleanup-test-data.ps1
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

Write-Host "Cleaning up test data..." -ForegroundColor Blue
Write-Host ([char]0x2501 * 36) -ForegroundColor Blue

# Load .env file if it exists
$envFile = Join-Path $projectRoot '.env'
if (Test-Path $envFile) {
    Get-Content $envFile | ForEach-Object {
        if ($_ -and -not $_.StartsWith('#')) {
            $parts = $_ -split '=', 2
            if ($parts.Count -eq 2) {
                $name = $parts[0].Trim()
                $value = $parts[1].Trim()
                [Environment]::SetEnvironmentVariable($name, $value, 'Process')
            }
        }
    }
}

# Database connection details
$dbHost = if ($env:DB_HOST) { $env:DB_HOST } else { 'localhost' }
$dbPort = if ($env:DB_PORT) { $env:DB_PORT } else { '5432' }
$dbName = if ($env:DB_NAME) { $env:DB_NAME } else { 'conduit' }
$dbUser = if ($env:DB_USER) { $env:DB_USER } else { 'conduit' }
$dbPassword = if ($env:DB_PASSWORD) { $env:DB_PASSWORD } else { 'conduitpass' }

# Set password for psql
$env:PGPASSWORD = $dbPassword

Write-Host "Removing test data from database..." -ForegroundColor Yellow

# SQL command to delete test data
$sqlCleanup = @'
-- Delete test virtual keys
DELETE FROM "VirtualKeys" WHERE "VirtualKey" LIKE 'condt_%';

-- Delete test virtual key groups
DELETE FROM "VirtualKeyGroups" WHERE "Name" LIKE 'TEST_%';

-- Delete test model costs
DELETE FROM "ModelCosts" WHERE "Name" LIKE 'TEST_%';

-- Delete test model mappings
DELETE FROM "ModelProviderMappings" WHERE "ModelId" LIKE 'TEST_%';

-- Delete test provider keys
DELETE FROM "ProviderKeyCredentials" WHERE "KeyName" LIKE 'TEST_%';

-- Delete test providers
DELETE FROM "Providers" WHERE "ProviderName" LIKE 'TEST_%';
'@

# Execute cleanup - don't fail if database is not accessible
try {
    $tempSqlFile = [System.IO.Path]::GetTempFileName()
    $tempSqlFile = [System.IO.Path]::ChangeExtension($tempSqlFile, '.sql')
    Set-Content -Path $tempSqlFile -Value $sqlCleanup -Encoding UTF8

    $psqlResult = & psql -h $dbHost -p $dbPort -d $dbName -U $dbUser -f $tempSqlFile 2>&1

    if ($LASTEXITCODE -eq 0) {
        Write-Host "[OK] Test data cleaned" -ForegroundColor Green
    }
} catch {
    # Silently continue if database is not accessible
} finally {
    if (Test-Path $tempSqlFile) {
        Remove-Item $tempSqlFile -Force -ErrorAction SilentlyContinue
    }
}

# Clean test reports
$reportDir = Join-Path $projectRoot 'ConduitLLM.IntegrationTests' 'bin' 'Debug' 'net9.0' 'Reports'
if (Test-Path $reportDir) {
    Write-Host "Removing old test reports..." -ForegroundColor Yellow
    Get-ChildItem -Path $reportDir -Filter '*.md' -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    Write-Host "[OK] Test reports cleaned" -ForegroundColor Green
}

# Clean test context
$contextFile = Join-Path $projectRoot 'ConduitLLM.IntegrationTests' 'bin' 'Debug' 'net9.0' 'test-context.json'
if (Test-Path $contextFile) {
    Remove-Item $contextFile -Force
    Write-Host "[OK] Test context cleaned" -ForegroundColor Green
}

Write-Host ([char]0x2501 * 36) -ForegroundColor Blue
Write-Host "[OK] Cleanup complete!" -ForegroundColor Green
Write-Host ""

exit 0
