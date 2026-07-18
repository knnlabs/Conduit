#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    Create the WebAdmin virtual key.

.DESCRIPTION
    This script creates or retrieves the WebAdmin Internal Key used for
    SignalR connections and internal authentication.

.EXAMPLE
    ./scripts/dev/create-webadmin-key.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

# Import common utilities
$scriptDir = $PSScriptRoot
Import-Module (Join-Path $scriptDir 'lib' 'Common.psm1') -Force

# Wait for services
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

# Check if WebAdmin Internal Key exists
Write-Host "Checking for existing WebAdmin Internal Key..." -ForegroundColor Yellow

$headers = @{
    'X-Master-Key' = $masterKey
    'Content-Type' = 'application/json'
}

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5002/api/virtualkeys" -Method Get -Headers $headers -ErrorAction Stop

    # Look for WebAdmin Internal Key
    $existingKey = $response | Where-Object { $_.keyName -eq "WebAdmin Internal Key" }

    if ($existingKey) {
        Write-Host "WebAdmin Internal Key already exists (ID: $($existingKey.id))" -ForegroundColor Green
        Write-Host "Note: The actual key value can only be retrieved when creating the key." -ForegroundColor Yellow
        Write-Host "If you need the key value, please delete and recreate it." -ForegroundColor Yellow
        exit 0
    }
}
catch {
    # If API call fails, try to create the key anyway
    Write-Host "Could not check existing keys, attempting to create..." -ForegroundColor Yellow
}

# Create WebAdmin Internal Key
Write-Host "Creating WebAdmin Internal Key..." -ForegroundColor Yellow

$createPayload = @{
    keyName = "WebAdmin Internal Key"
    description = "Internal key for WebAdmin SignalR connections"
    isEnabled = $true
    metadata = '{"purpose": "Internal WebAdmin authentication", "createdBy": "create-webadmin-key-script"}'
} | ConvertTo-Json

try {
    $createResponse = Invoke-RestMethod -Uri "http://localhost:5002/api/virtualkeys" -Method Post -Headers $headers -Body $createPayload -ContentType 'application/json'
}
catch {
    $errorMessage = $_.Exception.Message
    if ($_.ErrorDetails.Message) {
        $errorMessage = $_.ErrorDetails.Message
    }
    Write-Host "Error creating key: $errorMessage" -ForegroundColor Red
    exit 1
}

# Extract the key
$virtualKey = $createResponse.virtualKey

if ([string]::IsNullOrWhiteSpace($virtualKey)) {
    Write-Host "Error: Failed to extract key from response" -ForegroundColor Red
    Write-Host "Response: $($createResponse | ConvertTo-Json -Depth 5)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "WebAdmin Internal Key created successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "IMPORTANT: Save this key - it cannot be retrieved again!" -ForegroundColor Yellow
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host $virtualKey -ForegroundColor White
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "To configure the WebAdmin, set this environment variable:" -ForegroundColor Yellow
Write-Host "  `$env:CONDUIT_WEBADMIN_VIRTUAL_KEY = `"$virtualKey`"" -ForegroundColor White
Write-Host ""

# Save to a temporary file for testing (cross-platform temp path)
$tempPath = Get-CrossPlatformTempPath
$tempFile = Join-Path $tempPath "webadmin-virtual-key.txt"
$virtualKey | Out-File -FilePath $tempFile -Encoding utf8 -NoNewline

Write-Host "Key saved to: $tempFile" -ForegroundColor Cyan
