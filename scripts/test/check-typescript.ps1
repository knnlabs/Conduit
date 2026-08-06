#!/usr/bin/env pwsh
#Requires -Version 7.0
[CmdletBinding()]
param([switch]$Fix, [switch]$Json)

$ErrorActionPreference = 'Stop'
$projectRoot = Resolve-Path (Join-Path $PSScriptRoot '..' '..')
$webAdmin = Join-Path $projectRoot 'WebAdmin'

Push-Location $webAdmin
try {
    if ($Fix) { & npm run lint:fix }
    & npm run type-check
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & npm run lint
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    if ($Json) { @{ project = 'WebAdmin'; success = $true } | ConvertTo-Json }
}
finally {
    Pop-Location
}
