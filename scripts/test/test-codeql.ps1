#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    CodeQL Local Testing Script.

.DESCRIPTION
    Tests CodeQL analysis locally to verify error counts before pushing to GitHub.

.PARAMETER Quick
    Run minimal analysis (faster, less comprehensive).

.PARAMETER Clean
    Force rebuild of CodeQL database.

.PARAMETER NoFilter
    Don't apply workflow query filters.

.EXAMPLE
    ./scripts/test/test-codeql.ps1

.EXAMPLE
    ./scripts/test/test-codeql.ps1 -Quick

.EXAMPLE
    ./scripts/test/test-codeql.ps1 -Clean
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$Quick,

    [Parameter()]
    [switch]$Clean,

    [Parameter()]
    [switch]$NoFilter
)

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

$codeqlDir = Join-Path $projectRoot '.codeql'
$codeqlDb = Join-Path $projectRoot 'codeql-db'
$resultsDir = Join-Path $projectRoot 'codeql-results'

Write-Host "=== CodeQL Local Testing Script ===" -ForegroundColor Blue
Write-Host ""

function Install-CodeQL {
    Write-Host "Installing CodeQL CLI..." -ForegroundColor Yellow

    # Get latest CodeQL bundle version from GitHub
    Write-Host "Fetching latest CodeQL version..."
    try {
        $release = Invoke-RestMethod -Uri 'https://api.github.com/repos/github/codeql-action/releases/latest' -ErrorAction Stop
        $latestVersion = $release.tag_name
    } catch {
        Write-Host "Failed to fetch latest version, using fallback" -ForegroundColor Red
        $latestVersion = "codeql-bundle-v2.20.5"
    }

    Write-Host "Latest version: $latestVersion"

    # Determine platform
    $platform = if ($IsWindows) { "win64" } elseif ($IsMacOS) { "osx64" } else { "linux64" }
    $downloadUrl = "https://github.com/github/codeql-action/releases/download/${latestVersion}/codeql-bundle-${platform}.tar.gz"
    Write-Host "Downloading from: $downloadUrl"

    if (-not (Test-Path $codeqlDir)) {
        New-Item -ItemType Directory -Path $codeqlDir -Force | Out-Null
    }

    $tarFile = Join-Path $codeqlDir 'codeql-bundle.tar.gz'

    try {
        Invoke-WebRequest -Uri $downloadUrl -OutFile $tarFile
    } catch {
        Write-Host "Failed to download CodeQL bundle" -ForegroundColor Red
        exit 1
    }

    Write-Host "Extracting CodeQL bundle..."
    Push-Location $codeqlDir
    try {
        tar -xzf 'codeql-bundle.tar.gz'
        Remove-Item 'codeql-bundle.tar.gz' -Force
    } finally {
        Pop-Location
    }

    Write-Host "CodeQL installed successfully" -ForegroundColor Green
}

# Check if CodeQL is installed
$codeqlPath = Join-Path $codeqlDir 'codeql'
if (-not (Test-Path $codeqlPath)) {
    Write-Host "CodeQL not found at $codeqlDir" -ForegroundColor Yellow
    Install-CodeQL
} else {
    Write-Host "CodeQL found at $codeqlDir" -ForegroundColor Green
    # Check version
    $codeqlExe = if ($IsWindows) { Join-Path $codeqlPath 'codeql.exe' } else { Join-Path $codeqlPath 'codeql' }
    & $codeqlExe version
}

# Add CodeQL to PATH for this session
$codeqlExe = if ($IsWindows) { Join-Path $codeqlPath 'codeql.exe' } else { Join-Path $codeqlPath 'codeql' }

Push-Location $projectRoot

try {
    # Clean old database if requested or doesn't exist
    if ($Clean -or -not (Test-Path $codeqlDb)) {
        Write-Host "Creating CodeQL database (this will take 5-10 minutes)..." -ForegroundColor Yellow

        if (Test-Path $codeqlDb) {
            Remove-Item $codeqlDb -Recurse -Force
        }

        # Create a temporary build script
        $buildScript = Join-Path $projectRoot '.codeql-build.ps1'
        $buildContent = @'
$ErrorActionPreference = 'Stop'
& dotnet clean --configuration Release
& dotnet build --configuration Release
'@
        Set-Content -Path $buildScript -Value $buildContent -Encoding UTF8

        & $codeqlExe database create $codeqlDb `
            --language=csharp `
            --source-root=$projectRoot `
            --command="pwsh -File $buildScript" `
            --overwrite

        # Clean up build script
        Remove-Item $buildScript -Force -ErrorAction SilentlyContinue

        if ($LASTEXITCODE -ne 0) {
            Write-Host "Failed to create CodeQL database" -ForegroundColor Red
            exit 1
        }

        Write-Host "Database created successfully" -ForegroundColor Green
    } else {
        Write-Host "Using existing database at $codeqlDb" -ForegroundColor Green
    }

    # Create results directory
    if (-not (Test-Path $resultsDir)) {
        New-Item -ItemType Directory -Path $resultsDir -Force | Out-Null
    }

    # Prepare query suite
    $querySuite = if ($Quick) {
        Write-Host "Running in quick mode (security queries only)" -ForegroundColor Yellow
        "csharp-security-extended.qls"
    } else {
        Write-Host "Running full analysis (security and quality)" -ForegroundColor Yellow
        "csharp-security-and-quality.qls"
    }

    # Create config file with filters if needed
    if (-not $NoFilter) {
        Write-Host "Applying workflow query filters..." -ForegroundColor Blue
        $configContent = @'
query-filters:
  - exclude:
      id: js/unused-local-variable
  - exclude:
      id: cs/static-field-written-by-instance
  - exclude:
      id: cs/loss-of-precision
      tags: test
  - exclude:
      id: cs/unused-collection
      tags: test
'@
        $configFile = Join-Path $resultsDir 'qlconfig.yml'
        Set-Content -Path $configFile -Value $configContent -Encoding UTF8
    }

    # Run analysis
    Write-Host "Running CodeQL analysis (this will take 10-15 minutes)..." -ForegroundColor Yellow
    Write-Host ""

    $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $sarifFile = Join-Path $resultsDir "results_${timestamp}.sarif"

    & $codeqlExe database analyze $codeqlDb `
        --format=sarif-latest `
        --output=$sarifFile `
        --sarif-category=/language:csharp `
        --sarif-add-query-help `
        $querySuite

    if ($LASTEXITCODE -ne 0) {
        Write-Host "CodeQL analysis failed" -ForegroundColor Red
        exit 1
    }

    Write-Host ""
    Write-Host "=== Analysis Complete ===" -ForegroundColor Green
    Write-Host ""

    # Generate summary
    Write-Host "=== Results Summary ===" -ForegroundColor Blue
    Write-Host ""

    # Count total issues
    $sarifContent = Get-Content $sarifFile -Raw | ConvertFrom-Json
    $results = $sarifContent.runs[0].results
    $totalIssues = $results.Count

    Write-Host "Total issues found: " -NoNewline
    Write-Host $totalIssues -ForegroundColor Yellow
    Write-Host ""

    # Show breakdown by severity
    Write-Host "Issues by severity:" -ForegroundColor Blue
    $results | Group-Object { $_.level } | Sort-Object Count -Descending | ForEach-Object {
        Write-Host "  $($_.Count) $($_.Name)"
    }

    Write-Host ""
    Write-Host "Top 20 issue types:" -ForegroundColor Blue
    $results | Group-Object ruleId | Sort-Object Count -Descending | Select-Object -First 20 | ForEach-Object {
        Write-Host "  $($_.Count) $($_.Name)"
    }

    Write-Host ""
    Write-Host "Error-level issues:" -ForegroundColor Blue
    $errorResults = $results | Where-Object { $_.level -eq 'error' }
    if ($errorResults) {
        $errorResults | Group-Object ruleId | Sort-Object Count -Descending | ForEach-Object {
            Write-Host "  $($_.Count) $($_.Name)"
        }
    } else {
        Write-Host "  No error-level issues found" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "Results saved to: $sarifFile" -ForegroundColor Green
    Write-Host ""

    Write-Host "=== Analysis Complete ===" -ForegroundColor Green
    Write-Host ""
    Write-Host "Compare with GitHub's count by checking:"
    Write-Host "https://github.com/knnlabs/Conduit/security/code-scanning"
    Write-Host ""

    # Show comparison with last run if available
    $previousSarifs = Get-ChildItem -Path $resultsDir -Filter 'results_*.sarif' | Sort-Object LastWriteTime -Descending | Select-Object -Skip 1 -First 1
    if ($previousSarifs) {
        $lastSarifContent = Get-Content $previousSarifs.FullName -Raw | ConvertFrom-Json
        $lastCount = $lastSarifContent.runs[0].results.Count
        $diff = $totalIssues - $lastCount

        Write-Host "Comparison with last run:" -ForegroundColor Blue
        Write-Host "Previous: $lastCount issues"
        Write-Host "Current:  $totalIssues issues"

        if ($diff -gt 0) {
            Write-Host "Change:   +$diff issues" -ForegroundColor Red
        } elseif ($diff -lt 0) {
            Write-Host "Change:   $diff issues" -ForegroundColor Green
        } else {
            Write-Host "Change:   No change" -ForegroundColor Yellow
        }
    }
} finally {
    Pop-Location
}
