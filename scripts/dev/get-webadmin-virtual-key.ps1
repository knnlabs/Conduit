#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    Get the WebAdmin Internal Virtual Key.

.DESCRIPTION
    This script retrieves the WebAdmin virtual key from GlobalSettings,
    or creates a new one if it doesn't exist.

.EXAMPLE
    ./scripts/dev/get-webadmin-virtual-key.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

# Import common utilities
$scriptDir = $PSScriptRoot
Import-Module (Join-Path $scriptDir 'lib' 'Common.psm1') -Force

# First, ensure services are running
$waitScript = Join-Path $scriptDir '..' 'setup' 'wait-for-services.ps1'
if (Test-Path $waitScript) {
    & $waitScript
    if ($LASTEXITCODE -ne 0) {
        exit 1
    }
}

# Get master key from environment
$masterKey = [Environment]::GetEnvironmentVariable('CONDUIT_MASTER_KEY')

if ([string]::IsNullOrWhiteSpace($masterKey)) {
    Write-Host "Error: CONDUIT_MASTER_KEY environment variable is not set" -ForegroundColor Red
    Write-Host "Please set CONDUIT_MASTER_KEY in your .env file or environment" -ForegroundColor Red
    exit 1
}

Write-Host "Using master key: $masterKey" -ForegroundColor Gray

# Try to get the WebAdmin virtual key from GlobalSettings
$headers = @{
    'X-API-Key' = $masterKey
    'Content-Type' = 'application/json'
}

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5002/api/GlobalSettings/by-key/WebAdmin_VirtualKey" -Method Get -Headers $headers -ErrorAction Stop

    if ($response -and $response.value) {
        Write-Host "Found WebAdmin virtual key in GlobalSettings" -ForegroundColor Green
        Write-Output $response.value
        exit 0
    }
}
catch {
    # Key not found in GlobalSettings, will create new one
    Write-Host "WebAdmin virtual key not found in GlobalSettings. Creating new one..." -ForegroundColor Yellow
}

# First, ensure we have a default virtual key group
try {
    $groupResponse = Invoke-RestMethod -Uri "http://localhost:5002/api/VirtualKeyGroups" -Method Get -Headers $headers -ErrorAction Stop
    $groupId = $groupResponse[0].id
}
catch {
    $groupId = $null
}

if (-not $groupId) {
    Write-Host "Creating default virtual key group..." -ForegroundColor Yellow

    $groupBody = @{
        name = "Default Group"
        description = "Default virtual key group"
    } | ConvertTo-Json

    try {
        $groupResponse = Invoke-RestMethod -Uri "http://localhost:5002/api/VirtualKeyGroups" -Method Post -Headers $headers -Body $groupBody -ContentType 'application/json'
        $groupId = $groupResponse.id
    }
    catch {
        Write-Host "Warning: Could not create virtual key group" -ForegroundColor Yellow
        $groupId = $null
    }
}

# Create a new virtual key
Write-Host "Creating new 'WebAdmin Internal Key'..." -ForegroundColor Yellow

$createPayload = @{
    keyName = "WebAdmin Internal Key"
    allowedModels = $null
    maxBudget = $null
    budgetDuration = $null
    expiresAt = $null
    virtualKeyGroupId = $groupId
    metadata = '{"purpose": "Internal WebAdmin authentication"}'
    rateLimitRpm = $null
    rateLimitRpd = $null
} | ConvertTo-Json

try {
    $createResponse = Invoke-RestMethod -Uri "http://localhost:5002/api/VirtualKeys" -Method Post -Headers $headers -Body $createPayload -ContentType 'application/json'
}
catch {
    $errorMessage = $_.Exception.Message
    if ($_.ErrorDetails.Message) {
        $errorMessage = $_.ErrorDetails.Message
    }
    Write-Host "Error: Failed to create new WebAdmin key. API response:" -ForegroundColor Red
    Write-Host $errorMessage -ForegroundColor Red
    exit 1
}

# Extract the new key
$webAdminKey = $createResponse.virtualKey

if ([string]::IsNullOrWhiteSpace($webAdminKey)) {
    Write-Host "Error: Failed to extract new key from API response." -ForegroundColor Red
    Write-Host ($createResponse | ConvertTo-Json -Depth 5) -ForegroundColor Red
    exit 1
}

# Store the key in GlobalSettings for future use
Write-Host "Storing WebAdmin key in GlobalSettings..." -ForegroundColor Yellow

$storePayload = @{
    key = "WebAdmin_VirtualKey"
    value = $webAdminKey
    description = "Virtual key for WebAdmin Gateway API access"
} | ConvertTo-Json

try {
    $null = Invoke-RestMethod -Uri "http://localhost:5002/api/GlobalSettings" -Method Post -Headers $headers -Body $storePayload -ContentType 'application/json'
    Write-Host "Successfully stored WebAdmin key in GlobalSettings." -ForegroundColor Green
}
catch {
    Write-Host "Warning: Failed to store key in GlobalSettings, but key was created." -ForegroundColor Yellow
}

Write-Output $webAdminKey
