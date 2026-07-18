#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    GitHub Actions Workflow Validation Script.

.DESCRIPTION
    This script validates all GitHub Actions workflow files locally
    to catch issues before they fail in GitHub.

.EXAMPLE
    ./scripts/test/validate-workflows.ps1
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

$workflowsDir = Join-Path $projectRoot '.github' 'workflows'

# Tracking variables
$script:totalErrors = 0
$script:totalWarnings = 0
$script:workflowsChecked = 0

function Write-Status {
    param(
        [ValidateSet('error', 'success', 'warning', 'info', 'header')]
        [string]$Status,
        [string]$Message
    )

    switch ($Status) {
        'error'   { Write-Host "X $Message" -ForegroundColor Red }
        'success' { Write-Host "[OK] $Message" -ForegroundColor Green }
        'warning' { Write-Host "[!] $Message" -ForegroundColor Yellow }
        'info'    { Write-Host "[i] $Message" -ForegroundColor Cyan }
        'header'  {
            Write-Host ""
            Write-Host "==============================================" -ForegroundColor Blue
            Write-Host $Message -ForegroundColor Blue
            Write-Host "==============================================" -ForegroundColor Blue
        }
    }
}

function Test-CommandExists {
    param([string]$Command)
    return $null -ne (Get-Command $Command -ErrorAction SilentlyContinue)
}

function Test-YamlSyntax {
    param([string]$FilePath)

    $filename = Split-Path $FilePath -Leaf
    Write-Host "Checking $filename..." -ForegroundColor Yellow

    # Check if file exists
    if (-not (Test-Path $FilePath)) {
        Write-Status 'error' "File not found: $FilePath"
        $script:totalErrors++
        return $false
    }

    $content = Get-Content $FilePath -Raw

    # Basic YAML validation
    if ($content -notmatch '(?m)^name:') {
        Write-Status 'error' "Missing 'name' field in $filename"
        $script:totalErrors++
    }
    if ($content -notmatch '(?m)^on:') {
        Write-Status 'error' "Missing 'on' trigger in $filename"
        $script:totalErrors++
    }

    Write-Status 'success' "Valid YAML syntax"
    return $true
}

function Test-PathReferences {
    param([string]$FilePath)

    $content = Get-Content $FilePath -Raw
    $filename = Split-Path $FilePath -Leaf

    # Check for old client paths (should be SDKs)
    if ($content -match 'NodeClients/|Clients/Node/') {
        Write-Status 'error' "Found outdated client paths (should be SDKs/Node/*):"
        $script:totalErrors++
    }

    # Check for correct SDK paths
    if ($content -match 'SDKs/Node/(Admin|Core|Common)') {
        Write-Status 'success' "Using correct SDK paths"
    }
}

function Test-Runners {
    param([string]$FilePath)

    $content = Get-Content $FilePath -Raw

    # Check for non-existent ARM64 runners
    if ($content -match '(?m)^\s*runs-on:.*arm|^\s*runs-on:.*ubuntu-.*-arm') {
        Write-Status 'error' "Found ARM64 runner (not supported by GitHub Actions)"
        $script:totalErrors++
    }

    # Check for valid runners
    if ($content -match 'runs-on:\s*(ubuntu-latest|ubuntu-2[0-9]\.[0-9]{2}|windows-latest|macos-latest)') {
        Write-Status 'success' "Using valid GitHub-hosted runners"
    }
}

function Test-Secrets {
    param([string]$FilePath)

    $content = Get-Content $FilePath -Raw
    $matches = [regex]::Matches($content, '\$\{\{\s*secrets\.([A-Z_]+)\s*\}\}')

    $requiredSecrets = @()
    foreach ($match in $matches) {
        $secretName = $match.Groups[1].Value
        if ($secretName -ne 'GITHUB_TOKEN') {
            $requiredSecrets += $secretName
        }
    }

    if ($requiredSecrets.Count -gt 0) {
        $uniqueSecrets = $requiredSecrets | Select-Object -Unique
        Write-Status 'info' "Required secrets: $($uniqueSecrets -join ', ')"
        Write-Host "    Make sure these are configured in repository settings" -ForegroundColor Yellow
    }
}

function Test-DeprecatedActions {
    param([string]$FilePath)

    $content = Get-Content $FilePath -Raw

    # Check for old action versions
    if ($content -match 'actions/checkout@v[1-3]|actions/setup-node@v[1-3]') {
        Write-Status 'warning' "Using older action versions (consider updating to v4+)"
        $script:totalWarnings++
    }
}

function Test-Concurrency {
    param([string]$FilePath)

    $content = Get-Content $FilePath -Raw

    # Check for versioning/publishing workflows without proper concurrency control
    if ($content -match 'version|publish|release') {
        if ($content -notmatch 'cancel-in-progress:\s*false') {
            Write-Status 'warning' "Version/publish workflow should have 'cancel-in-progress: false'"
            $script:totalWarnings++
        } else {
            Write-Status 'success' "Proper concurrency control for versioning"
        }
    }
}

function Test-DockerManifests {
    param([string]$FilePath)

    $content = Get-Content $FilePath -Raw

    if ($content -match 'docker-manifest|imagetools create') {
        # Check for ARM64 references that should be removed
        if ($content -match 'arm64|linux/arm64') {
            Write-Status 'warning' "Found ARM64 references in Docker manifest (ARM64 builds removed)"
            $script:totalWarnings++
        }
    }
}

function Test-ScriptReferences {
    param([string]$FilePath)

    $content = Get-Content $FilePath -Raw
    $matches = [regex]::Matches($content, '(?:\./)?scripts/([^\s]+\.sh)')

    foreach ($match in $matches) {
        $scriptPath = $match.Groups[1].Value
        $fullPath = Join-Path $projectRoot 'scripts' $scriptPath
        if (-not (Test-Path $fullPath)) {
            Write-Status 'warning' "Referenced script not found: scripts/$scriptPath"
            $script:totalWarnings++
        }
    }
}

function Test-Workflow {
    param([string]$FilePath)

    Test-YamlSyntax -FilePath $FilePath
    Test-PathReferences -FilePath $FilePath
    Test-Runners -FilePath $FilePath
    Test-Secrets -FilePath $FilePath
    Test-DeprecatedActions -FilePath $FilePath
    Test-Concurrency -FilePath $FilePath
    Test-DockerManifests -FilePath $FilePath
    Test-ScriptReferences -FilePath $FilePath

    $script:workflowsChecked++
}

# Main execution
Write-Status 'header' "GitHub Actions Workflow Validation"

# Check if workflows directory exists
if (-not (Test-Path $workflowsDir)) {
    Write-Status 'error' "Workflows directory not found at $workflowsDir"
    exit 1
}

# Check for yq installation
if (-not (Test-CommandExists 'yq')) {
    Write-Status 'info' "'yq' not found. Install it for better YAML validation:"
    Write-Host "  choco install yq  # Windows"
    Write-Host "  brew install yq   # macOS"
    Write-Host "  sudo snap install yq  # Ubuntu"
    Write-Host ""
}

# Validate each workflow file
Write-Status 'header' "Validating Workflow Files"

$workflowFiles = Get-ChildItem -Path $workflowsDir -Filter '*.yml' -ErrorAction SilentlyContinue
$workflowFiles += Get-ChildItem -Path $workflowsDir -Filter '*.yaml' -ErrorAction SilentlyContinue

foreach ($workflow in $workflowFiles) {
    Test-Workflow -FilePath $workflow.FullName
    Write-Host ""
}

# Summary
Write-Status 'header' "Validation Summary"

Write-Host "Workflows checked: $($script:workflowsChecked)" -ForegroundColor Blue

if ($script:totalErrors -eq 0 -and $script:totalWarnings -eq 0) {
    Write-Status 'success' "All workflows validated successfully!"
    Write-Host "No issues found. Safe to push to GitHub." -ForegroundColor Green
} else {
    if ($script:totalErrors -gt 0) {
        Write-Status 'error' "Found $($script:totalErrors) error(s)"
        Write-Host "These MUST be fixed before pushing to GitHub" -ForegroundColor Red
    }
    if ($script:totalWarnings -gt 0) {
        Write-Status 'warning' "Found $($script:totalWarnings) warning(s)"
        Write-Host "Consider addressing these warnings" -ForegroundColor Yellow
    }
}

Write-Host ""

# Exit with error if any errors found
if ($script:totalErrors -gt 0) {
    exit 1
}

exit 0
