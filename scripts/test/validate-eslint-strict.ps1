#!/usr/bin/env pwsh
#Requires -Version 5.1
<#
.SYNOPSIS
    Strict ESLint validation wrapper script.

.DESCRIPTION
    This script maintains backward compatibility by calling the unified script with -Strict flag.
    This is what CI/CD uses and the pre-push hook calls.

.EXAMPLE
    ./scripts/test/validate-eslint-strict.ps1
#>

# Get the directory of this script
$scriptDir = $PSScriptRoot

# Call the unified validation script with -Strict flag
& "$scriptDir/validate-eslint.ps1" -Strict @args
