#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    Create a test virtual key for SignalR connections.

.DESCRIPTION
    This script creates a virtual key for testing purposes, particularly for
    SignalR connections. Requires CONDUIT_MASTER_KEY environment variable.

.PARAMETER KeyName
    The name for the virtual key. Default is "SignalR Test Key".

.PARAMETER Description
    Description for the virtual key. Default is "Test key for SignalR connections".

.EXAMPLE
    ./scripts/dev/create-test-virtual-key.ps1

.EXAMPLE
    ./scripts/dev/create-test-virtual-key.ps1 -KeyName "My Test Key" -Description "Custom test key"
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$KeyName = "SignalR Test Key",

    [Parameter(Position = 1)]
    [string]$Description = "Test key for SignalR connections"
)

$ErrorActionPreference = 'Stop'

# Import common utilities
$scriptDir = $PSScriptRoot
Import-Module (Join-Path $scriptDir 'lib' 'Common.psm1') -Force

# Get master key from environment
$masterKey = [Environment]::GetEnvironmentVariable('CONDUIT_MASTER_KEY')

if ([string]::IsNullOrWhiteSpace($masterKey)) {
    Write-Host "Error: CONDUIT_MASTER_KEY environment variable is not set" -ForegroundColor Red
    Write-Host "Please set CONDUIT_MASTER_KEY in your .env file or environment" -ForegroundColor Red
    exit 1
}

Write-Host "Creating virtual key: $KeyName" -ForegroundColor Yellow

# Create the virtual key
$uri = "http://localhost:5002/api/virtualkeys"
$headers = @{
    'X-Master-Key' = $masterKey
    'Content-Type' = 'application/json'
}

$body = @{
    keyName = $KeyName
    description = $Description
    isEnabled = $true
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri $uri -Method Post -Headers $headers -Body $body -ContentType 'application/json'
}
catch {
    $errorMessage = $_.Exception.Message
    if ($_.ErrorDetails.Message) {
        $errorMessage = $_.ErrorDetails.Message
    }
    Write-Host "Error: Failed to create virtual key" -ForegroundColor Red
    Write-Host "API Error: $errorMessage" -ForegroundColor Red
    exit 1
}

# Extract the key
$virtualKey = $response.virtualKey

if ([string]::IsNullOrWhiteSpace($virtualKey)) {
    Write-Host "Error: Failed to extract key from response" -ForegroundColor Red
    Write-Host "Response: $($response | ConvertTo-Json -Depth 5)" -ForegroundColor Red
    exit 1
}

Write-Host "Created virtual key successfully!" -ForegroundColor Green
Write-Host "Key Name: $KeyName" -ForegroundColor Cyan
Write-Host "Key Value: $virtualKey" -ForegroundColor Cyan
Write-Host ""

# Output just the key for piping
Write-Output $virtualKey
