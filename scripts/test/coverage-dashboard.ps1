#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    Coverage Dashboard Script.

.DESCRIPTION
    Generates comprehensive coverage reports and analysis.

.PARAMETER Command
    The command to run: run, report, or summary.

.EXAMPLE
    ./scripts/test/coverage-dashboard.ps1 run

.EXAMPLE
    ./scripts/test/coverage-dashboard.ps1 report

.EXAMPLE
    ./scripts/test/coverage-dashboard.ps1 summary
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('run', 'report', 'summary')]
    [string]$Command
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

Write-Host "[i] ConduitLLM Coverage Dashboard" -ForegroundColor Blue
Write-Host "================================"

# Configuration
$coverageDir = Join-Path $projectRoot 'TestResults'
$reportDir = Join-Path $projectRoot 'CoverageReport'

Push-Location $projectRoot

try {
    function Invoke-CoverageRun {
        Write-Host "[i] Running tests with coverage collection..." -ForegroundColor Blue

        # Clean previous results
        if (Test-Path $coverageDir) { Remove-Item $coverageDir -Recurse -Force }
        if (Test-Path $reportDir) { Remove-Item $reportDir -Recurse -Force }

        # Restore tools if needed
        $toolsManifest = Join-Path $projectRoot '.config' 'dotnet-tools.json'
        if (-not (Test-Path $toolsManifest)) {
            Write-Host "[!] No local tools manifest found. Creating one..." -ForegroundColor Yellow
            $configDir = Join-Path $projectRoot '.config'
            if (-not (Test-Path $configDir)) {
                New-Item -ItemType Directory -Path $configDir -Force | Out-Null
            }
            $toolsContent = @'
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "dotnet-reportgenerator-globaltool": {
      "version": "5.3.11",
      "commands": [
        "reportgenerator"
      ]
    }
  }
}
'@
            Set-Content -Path $toolsManifest -Value $toolsContent -Encoding UTF8
        }

        Write-Host "[i] Restoring tools..." -ForegroundColor Blue
        & dotnet tool restore

        Write-Host "[i] Running tests..." -ForegroundColor Blue
        $runsettings = Join-Path $projectRoot '.runsettings'
        & dotnet test --configuration Release `
            --logger "console;verbosity=normal" `
            --collect:"XPlat Code Coverage" `
            --results-directory $coverageDir `
            --settings $runsettings

        Write-Host "[OK] Tests completed" -ForegroundColor Green
    }

    function Invoke-GenerateReports {
        Write-Host "[i] Generating coverage reports..." -ForegroundColor Blue

        # Find coverage files
        $coverageFiles = Get-ChildItem -Path $coverageDir -Filter 'coverage.cobertura.xml' -Recurse -ErrorAction SilentlyContinue

        if (-not $coverageFiles) {
            Write-Host "X No coverage files found!" -ForegroundColor Red
            Write-Host "Expected location: $coverageDir/**/coverage.cobertura.xml"
            exit 1
        }

        Write-Host "Found coverage files:" -ForegroundColor Green
        $coverageFiles | ForEach-Object { Write-Host $_.FullName }

        # Generate comprehensive reports
        $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
        $historyDir = Join-Path $reportDir 'history'

        & dotnet tool run reportgenerator `
            "-reports:$coverageDir/**/coverage.cobertura.xml" `
            "-targetdir:$reportDir" `
            "-reporttypes:Html;HtmlSummary;Badges;TextSummary;Cobertura;JsonSummary;MarkdownSummary" `
            "-assemblyfilters:+ConduitLLM.*;-*.Tests*;-*Test*" `
            "-classfilters:-*.Migrations*;-*.Program;-*.Startup" `
            "-filefilters:-**/Migrations/**;-**/Program.cs;-**/Startup.cs" `
            "-verbosity:Info" `
            "-title:Conduit LLM Coverage Report" `
            "-tag:$timestamp" `
            "-historydir:$historyDir"

        Write-Host "[OK] Reports generated in $reportDir" -ForegroundColor Green
    }

    function Show-Summary {
        Write-Host "[i] Coverage Summary" -ForegroundColor Blue
        Write-Host "==================="

        $summaryFile = Join-Path $reportDir 'Summary.json'
        if (-not (Test-Path $summaryFile)) {
            Write-Host "X Summary file not found!" -ForegroundColor Red
            return $false
        }

        # Parse coverage data
        $json = Get-Content $summaryFile -Raw | ConvertFrom-Json
        $lineCoverage = if ($json.summary.linecoverage) { [double]$json.summary.linecoverage } else { 0 }
        $branchCoverage = if ($json.summary.branchcoverage) { [double]$json.summary.branchcoverage } else { 0 }
        $methodCoverage = if ($json.summary.methodcoverage) { [double]$json.summary.methodcoverage } else { 0 }

        # Display overall coverage
        Write-Host ""
        Write-Host "Overall Coverage:" -ForegroundColor Blue
        Write-Host "  Line Coverage:   $lineCoverage%"
        Write-Host "  Branch Coverage: $branchCoverage%"
        Write-Host "  Method Coverage: $methodCoverage%"
        Write-Host ""

        # Coverage assessment
        if ($lineCoverage -ge 80) {
            Write-Host "Excellent coverage! (>=80%)" -ForegroundColor Green
        } elseif ($lineCoverage -ge 60) {
            Write-Host "Good coverage (60-79%)" -ForegroundColor Yellow
        } elseif ($lineCoverage -ge 40) {
            Write-Host "Moderate coverage (40-59%)" -ForegroundColor Yellow
        } else {
            Write-Host "Low coverage (<40%)" -ForegroundColor Red
            Write-Host "Consider adding more tests!" -ForegroundColor Red
        }

        Write-Host ""
        Write-Host "Coverage by Project:" -ForegroundColor Blue
        Write-Host "===================="

        # Project-specific coverage
        if ($json.coverage -and $json.coverage.assemblies) {
            foreach ($assembly in $json.coverage.assemblies) {
                if ($assembly.name -like '*ConduitLLM*') {
                    Write-Host "  $($assembly.name): $($assembly.coverage)%"
                }
            }
        } else {
            Write-Host "  Coverage details unavailable"
        }

        Write-Host ""
        Write-Host "Critical Services Analysis:" -ForegroundColor Blue
        Write-Host "==========================="

        # Analyze critical services
        $threshold = 80

        function Test-CriticalService {
            param(
                [string]$Name,
                [string]$Pattern,
                [object]$Json
            )

            $coverage = 0
            if ($Json.coverage -and $Json.coverage.assemblies) {
                $assembly = $Json.coverage.assemblies | Where-Object { $_.name -like "*$Pattern*" } | Select-Object -First 1
                if ($assembly) {
                    $coverage = [double]$assembly.coverage
                }
            }

            Write-Host -NoNewline "  ${Name}: $coverage%"
            if ($coverage -ge $threshold) {
                Write-Host " [OK]" -ForegroundColor Green
            } else {
                Write-Host " X (Target: $threshold%)" -ForegroundColor Red
            }
        }

        Test-CriticalService -Name "Core Services" -Pattern "ConduitLLM.Core" -Json $json
        Test-CriticalService -Name "Gateway API" -Pattern "ConduitLLM.Gateway" -Json $json
        Test-CriticalService -Name "Admin API" -Pattern "ConduitLLM.Admin" -Json $json

        return $true
    }

    function Open-Reports {
        Write-Host ""
        Write-Host "Available Reports:" -ForegroundColor Blue
        Write-Host "  HTML Report:     $reportDir/index.html"
        Write-Host "  Text Summary:    $reportDir/Summary.txt"
        Write-Host "  JSON Summary:    $reportDir/Summary.json"
        Write-Host "  Badges:          $reportDir/badge_linecoverage.svg"

        # Try to open HTML report
        $htmlReport = Join-Path $reportDir 'index.html'
        if (Test-Path $htmlReport) {
            Write-Host ""
            Write-Host "Opening HTML report..." -ForegroundColor Green
            Start-Process $htmlReport
        }
    }

    # Main execution
    switch ($Command) {
        'run' {
            Invoke-CoverageRun
            Invoke-GenerateReports
            Show-Summary
            Open-Reports
        }
        'report' {
            if (Test-Path $coverageDir) {
                Invoke-GenerateReports
                Show-Summary
                Open-Reports
            } else {
                Write-Host "X No coverage data found. Run with 'run' first." -ForegroundColor Red
                exit 1
            }
        }
        'summary' {
            if (Test-Path (Join-Path $reportDir 'Summary.json')) {
                Show-Summary
            } else {
                Write-Host "X No coverage summary found. Run coverage first." -ForegroundColor Red
                exit 1
            }
        }
        default {
            Write-Host "Usage: $($MyInvocation.MyCommand.Name) {run|report|summary}"
            Write-Host ""
            Write-Host "Commands:"
            Write-Host "  run     - Run tests with coverage and generate reports"
            Write-Host "  report  - Generate reports from existing coverage data"
            Write-Host "  summary - Display coverage summary from existing reports"
            Write-Host ""
            Write-Host "Examples:"
            Write-Host "  $($MyInvocation.MyCommand.Name) run      # Full coverage analysis"
            Write-Host "  $($MyInvocation.MyCommand.Name) summary  # Quick coverage check"
            exit 1
        }
    }
} finally {
    Pop-Location
}
