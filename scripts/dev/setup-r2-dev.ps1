#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    Setup Cloudflare R2 development environment.

.DESCRIPTION
    This script validates R2 configuration and starts the development environment
    with Cloudflare R2 storage.

.EXAMPLE
    ./scripts/dev/setup-r2-dev.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

# Import common utilities
$scriptDir = $PSScriptRoot
Import-Module (Join-Path $scriptDir 'lib' 'Common.psm1') -Force

Write-Host "=== Cloudflare R2 Development Setup ===" -ForegroundColor Cyan
Write-Host ""

# Get project root
$projectRoot = Get-ProjectRoot -FromPath $scriptDir
$envFile = Join-Path $projectRoot '.env'
$envTemplate = Join-Path $projectRoot '.env.r2.development'

# Check if .env exists
if (-not (Test-Path $envFile)) {
    Write-Err "No .env file found!"
    Write-Host "Creating .env from template..." -ForegroundColor Yellow

    if (-not (Test-Path $envTemplate)) {
        Write-Err "Template file .env.r2.development not found!"
        exit 1
    }

    Copy-Item $envTemplate $envFile

    Write-Host ""
    Write-Warn "Please edit .env and add your R2 credentials:"
    Write-Host "   1. Go to Cloudflare Dashboard -> R2"
    Write-Host "   2. Create a bucket called 'conduit-media-dev'"
    Write-Host "   3. Create an API token with R2 read/write permissions"
    Write-Host "   4. Add the credentials to .env"
    Write-Host ""
    Write-Host "Then run this script again."
    exit 1
}

# Load the .env file
Import-DotEnv -Path $envFile

# Validate R2 configuration
$s3Endpoint = [Environment]::GetEnvironmentVariable('CONDUIT_S3_ENDPOINT')
$s3AccessKey = [Environment]::GetEnvironmentVariable('CONDUIT_S3_ACCESS_KEY')
$s3SecretKey = [Environment]::GetEnvironmentVariable('CONDUIT_S3_SECRET_KEY')
$s3BucketName = [Environment]::GetEnvironmentVariable('CONDUIT_S3_BUCKET_NAME')
$s3PublicUrl = [Environment]::GetEnvironmentVariable('CONDUIT_S3_PUBLIC_BASE_URL')

if ([string]::IsNullOrWhiteSpace($s3Endpoint) -or $s3Endpoint -like '*<your-account-id>*') {
    Write-Err "R2 endpoint not configured in .env"
    Write-Host "   Please add your R2 endpoint URL"
    exit 1
}

if ([string]::IsNullOrWhiteSpace($s3AccessKey) -or $s3AccessKey -like '*<your-r2-access-key>*') {
    Write-Err "R2 access key not configured in .env"
    Write-Host "   Please add your R2 credentials"
    exit 1
}

Write-Success "R2 Configuration:"
Write-Host "   Endpoint: $s3Endpoint"
Write-Host "   Bucket: $s3BucketName"
Write-Host "   Public URL: $s3PublicUrl"
Write-Host ""

# Test R2 connectivity (optional)
Write-Host "Testing R2 connectivity..." -ForegroundColor Yellow

$awsCommand = Get-Command aws -ErrorAction SilentlyContinue
if ($awsCommand) {
    try {
        $env:AWS_ACCESS_KEY_ID = $s3AccessKey
        $env:AWS_SECRET_ACCESS_KEY = $s3SecretKey

        $null = aws s3 ls "s3://$s3BucketName" --endpoint-url $s3Endpoint --region auto 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Success "R2 connection successful!"
        }
        else {
            Write-Warn "Could not connect to R2 (this might be normal if bucket doesn't exist yet)"
        }
    }
    catch {
        Write-Warn "Could not connect to R2: $_"
    }
    finally {
        Remove-Item Env:AWS_ACCESS_KEY_ID -ErrorAction SilentlyContinue
        Remove-Item Env:AWS_SECRET_ACCESS_KEY -ErrorAction SilentlyContinue
    }
}
else {
    Write-Host "AWS CLI not installed, skipping connectivity test" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "Starting development environment with R2..." -ForegroundColor Cyan
Write-Host ""

# Start with R2 configuration
Push-Location $projectRoot
try {
    docker compose -f docker-compose.dev.yml up -d
    if ($LASTEXITCODE -ne 0) {
        Write-Err "Failed to start development environment"
        exit 1
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Success "Development environment started with Cloudflare R2!"
Write-Host ""
Write-Host "Services:" -ForegroundColor Cyan
Write-Host "   - WebAdmin: http://localhost:3000"
Write-Host "   - Gateway API: http://localhost:5000/swagger"
Write-Host "   - Admin API: http://localhost:5002/swagger"
Write-Host "   - Media Storage: Cloudflare R2"
Write-Host ""
Write-Host "Generated images will be stored in R2 and served from:" -ForegroundColor Cyan
Write-Host "   $s3PublicUrl"
Write-Host ""
