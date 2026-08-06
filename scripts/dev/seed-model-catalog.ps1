#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    Seeds the local development database with every checked-in provider model catalog.

.DESCRIPTION
    Calls the Admin API importer backed by the provider catalogs embedded in the
    running release. It never fetches a live catalog, so startup is deterministic.

    By default the script does nothing when model identifiers already exist. Use -Force
    to re-import the checked-in catalogs after their model data changes.
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$scriptDir = $PSScriptRoot
Import-Module (Join-Path $scriptDir 'lib' 'Common.psm1') -Force
$projectRoot = Get-ProjectRoot -FromPath $scriptDir
$providersDirectory = Join-Path $projectRoot 'scripts' 'db' 'providers'
$providerConfigPath = Join-Path $providersDirectory 'provider-config.json'
$composeArgs = @('compose', '-f', 'docker-compose.yml', '-f', 'docker-compose.dev.yml')

if (-not (Test-Path $providerConfigPath)) {
    throw "Provider catalog configuration was not found: $providerConfigPath"
}

# The APIs complete their migrations before dev.ps1 calls this script. Query the
# identifier table rather than Models: a catalog is useful only when it is mapped to
# a provider type.
Push-Location $projectRoot
try {
    $existingIdentifierCount = (& docker @composeArgs exec -T postgres psql -U conduit -d conduitdb -tAc 'SELECT COUNT(*) FROM "ModelIdentifiers";' | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not query ModelIdentifiers. Ensure the development services are healthy and migrations have completed.'
    }
}
finally {
    Pop-Location
}

if (-not $Force -and [int]$existingIdentifierCount -gt 0) {
    Write-Info "Model catalog already contains $existingIdentifierCount identifiers; skipping seed. Use -Force to re-import checked-in catalogs."
    exit 0
}

$backendKey = [Environment]::GetEnvironmentVariable('CONDUIT_API_TO_API_BACKEND_AUTH_KEY')
if ([string]::IsNullOrWhiteSpace($backendKey)) {
    $backendKey = 'alpha'
}

Write-Info 'Importing the bundled provider model catalogs through the Admin API...'
$result = Invoke-RestMethod `
    -Method Post `
    -Uri 'http://localhost:5002/api/Model/bundled-catalog/import' `
    -Headers @{ 'X-API-Key' = $backendKey } `
    -ContentType 'application/json' `
    -Body '{}'

Write-Success (
    "Bundled catalog import complete: {0} identifiers created, {1} existing identifiers preserved, {2} conflicts." -f `
        $result.created.identifiers, $result.skippedExistingIdentifiers, $result.conflicts.Count)
