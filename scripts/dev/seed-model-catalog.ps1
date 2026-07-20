#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    Seeds the local development database with every checked-in provider model catalog.

.DESCRIPTION
    Catalogs are discovered from scripts/db/providers/provider-config.json and the
    corresponding {provider}-models.json files. This uses the checked-in OpenRouter
    snapshot; it never fetches a live catalog, so development startup is deterministic.

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

$providerConfig = Get-Content $providerConfigPath -Raw | ConvertFrom-Json
$catalogProviders = @(
    $providerConfig.PSObject.Properties.Name |
        Where-Object { Test-Path (Join-Path $providersDirectory "$_-models.json") }
)

if ($catalogProviders.Count -eq 0) {
    throw "No checked-in provider model catalogs were found in $providersDirectory"
}

$temporaryDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("conduit-model-catalog-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null

try {
    $seededCatalogs = @()

    foreach ($provider in $catalogProviders) {
        $catalogPath = Join-Path $providersDirectory "$provider-models.json"
        $catalog = Get-Content $catalogPath -Raw | ConvertFrom-Json
        $modelCount = @($catalog.models.PSObject.Properties).Count
        $sqlPath = Join-Path $temporaryDirectory "$provider-models.sql"

        Write-Info "Generating $provider catalog ($modelCount models)..."
        Push-Location $providersDirectory
        try {
            & dotnet run .\generate-provider-sql.cs -- $provider $sqlPath
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to generate SQL for the $provider catalog"
            }
        }
        finally {
            Pop-Location
        }

        Write-Info "Importing $provider catalog..."
        Push-Location $projectRoot
        try {
            Get-Content $sqlPath -Raw | & docker @composeArgs exec -T postgres psql -v 'ON_ERROR_STOP=1' -U conduit -d conduitdb
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to import the $provider catalog"
            }
        }
        finally {
            Pop-Location
        }
        $seededCatalogs += "$provider ($modelCount)"
    }

    Write-Success "Seeded model catalogs: $($seededCatalogs -join ', ')."
}
finally {
    Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force -ErrorAction SilentlyContinue
}
