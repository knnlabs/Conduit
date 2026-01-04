#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    Fix production database migration issues.

.DESCRIPTION
    WARNING: This script should be run with extreme caution in production.
    Diagnoses and fixes common migration problems without deleting data.

.EXAMPLE
    ./scripts/migrations/fix-production-migrations.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

# Import common utilities
$scriptDir = $PSScriptRoot
$devLibPath = Join-Path $scriptDir '..' 'dev' 'lib' 'Common.psm1'
if (Test-Path $devLibPath) {
    Import-Module $devLibPath -Force
}

# Helper functions for colored output
function Write-MigrationStatus {
    param(
        [ValidateSet('error', 'success', 'warning', 'info')]
        [string]$Status,
        [string]$Message
    )

    switch ($Status) {
        'error'   { Write-Host "X ERROR: $Message" -ForegroundColor Red }
        'success' { Write-Host "[OK] $Message" -ForegroundColor Green }
        'warning' { Write-Host "[!] $Message" -ForegroundColor Yellow }
        'info'    { Write-Host "[i] $Message" -ForegroundColor Blue }
    }
}

# Validate environment
$databaseUrl = $env:DATABASE_URL
if ([string]::IsNullOrWhiteSpace($databaseUrl)) {
    Write-MigrationStatus 'error' "DATABASE_URL environment variable is not set"
    exit 1
}

# Extract database name from DATABASE_URL
if ($databaseUrl -match '/([^/?]+)(\?|$)') {
    $dbName = $Matches[1]
    Write-MigrationStatus 'info' "Working with database: $dbName"
} else {
    Write-MigrationStatus 'warning' "Could not extract database name from URL"
}

# Create temporary SQL file
$tempSql = [System.IO.Path]::GetTempFileName()
$tempSql = [System.IO.Path]::ChangeExtension($tempSql, '.sql')

try {
    # Write the SQL script
    $sqlScript = @'
-- Fix Production Migration Issues
-- This script diagnoses and fixes common migration problems

\echo '================================================'
\echo 'Database Migration Diagnostic Report'
\echo '================================================'
\echo ''

-- 1. Check if migrations table exists
\echo '1. Checking for migrations history table...'
SELECT CASE
    WHEN EXISTS (
        SELECT FROM information_schema.tables
        WHERE table_schema = 'public'
        AND table_name = '__EFMigrationsHistory'
    ) THEN 'FOUND: Migrations history table exists'
    ELSE 'MISSING: No migrations history table'
END AS migration_table_status;

-- 2. Check what migrations are recorded
\echo ''
\echo '2. Recorded migrations:'
SELECT "MigrationId", "ProductVersion"
FROM "__EFMigrationsHistory"
ORDER BY "MigrationId";

-- 3. Check if problematic tables exist
\echo ''
\echo '3. Checking for existing tables that might conflict:'
WITH table_checks AS (
    SELECT
        table_name,
        CASE WHEN EXISTS (
            SELECT FROM information_schema.tables t
            WHERE t.table_schema = 'public'
            AND t.table_name = tc.table_name
        ) THEN 'EXISTS' ELSE 'NOT FOUND' END AS status
    FROM (VALUES
        ('BatchOperationHistory'),
        ('MediaLifecycleRecords'),
        ('VirtualKeys'),
        ('GlobalSettings'),
        ('RequestLogs'),
        ('AsyncTasks')
    ) AS tc(table_name)
)
SELECT * FROM table_checks ORDER BY table_name;

-- 4. Determine the issue
\echo ''
\echo '4. Diagnosis:'
DO $$
DECLARE
    has_migration_table boolean;
    has_tables boolean;
    has_migration_entry boolean;
BEGIN
    -- Check migration table
    SELECT EXISTS (
        SELECT FROM information_schema.tables
        WHERE table_schema = 'public'
        AND table_name = '__EFMigrationsHistory'
    ) INTO has_migration_table;

    -- Check if any application tables exist
    SELECT EXISTS (
        SELECT FROM information_schema.tables
        WHERE table_schema = 'public'
        AND table_name IN ('VirtualKeys', 'BatchOperationHistory', 'MediaLifecycleRecords')
    ) INTO has_tables;

    -- Check if migration is recorded
    IF has_migration_table THEN
        SELECT EXISTS (
            SELECT FROM "__EFMigrationsHistory"
            WHERE "MigrationId" = '20250723043111_InitialCreate'
        ) INTO has_migration_entry;
    ELSE
        has_migration_entry := false;
    END IF;

    -- Diagnose the issue
    IF NOT has_migration_table AND has_tables THEN
        RAISE NOTICE 'ISSUE: Database was created with EnsureCreated (not migrations)';
        RAISE NOTICE 'FIX: Need to create migration history and mark migrations as applied';
    ELSIF has_migration_table AND NOT has_migration_entry AND has_tables THEN
        RAISE NOTICE 'ISSUE: Tables exist but migration not recorded';
        RAISE NOTICE 'FIX: Need to mark migration as applied';
    ELSIF has_migration_table AND has_migration_entry THEN
        RAISE NOTICE 'STATUS: Database appears to be properly configured';
    ELSE
        RAISE NOTICE 'STATUS: Clean database ready for migrations';
    END IF;
END $$;

-- 5. Proposed fix
\echo ''
\echo '5. Applying fixes...'

-- Create migration history table if missing
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM information_schema.tables
        WHERE table_schema = 'public'
        AND table_name = '__EFMigrationsHistory'
    ) THEN
        CREATE TABLE "__EFMigrationsHistory" (
            "MigrationId" VARCHAR(300) NOT NULL,
            "ProductVersion" VARCHAR(32) NOT NULL,
            CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
        );
        RAISE NOTICE 'Created __EFMigrationsHistory table';
    END IF;
END $$;

-- Mark migration as applied if tables exist but migration isn't recorded
DO $$
DECLARE
    tables_exist boolean;
    migration_exists boolean;
BEGIN
    -- Check if our tables exist
    SELECT EXISTS (
        SELECT FROM information_schema.tables
        WHERE table_schema = 'public'
        AND table_name IN ('BatchOperationHistory', 'MediaLifecycleRecords')
    ) INTO tables_exist;

    -- Check if migration is recorded
    SELECT EXISTS (
        SELECT FROM "__EFMigrationsHistory"
        WHERE "MigrationId" = '20250723043111_InitialCreate'
    ) INTO migration_exists;

    -- If tables exist but migration isn't recorded, record it
    IF tables_exist AND NOT migration_exists THEN
        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
        VALUES ('20250723043111_InitialCreate', '9.0.0')
        ON CONFLICT ("MigrationId") DO NOTHING;
        RAISE NOTICE 'Marked InitialCreate migration as applied';
    ELSIF migration_exists THEN
        RAISE NOTICE 'Migration already marked as applied';
    ELSE
        RAISE NOTICE 'No action needed - tables do not exist';
    END IF;
END $$;

-- 6. Final verification
\echo ''
\echo '6. Final verification:'
\echo ''
\echo 'Migration history:'
SELECT "MigrationId", "ProductVersion"
FROM "__EFMigrationsHistory"
ORDER BY "MigrationId";

\echo ''
\echo 'Table count:'
SELECT COUNT(*) as table_count
FROM information_schema.tables
WHERE table_schema = 'public'
AND table_name != '__EFMigrationsHistory';

\echo ''
\echo '================================================'
\echo 'Fix completed. Please restart your application.'
\echo '================================================'
'@

    Set-Content -Path $tempSql -Value $sqlScript -Encoding UTF8

    # Show what we're about to do
    Write-MigrationStatus 'warning' "This script will fix migration issues in your production database"
    Write-MigrationStatus 'warning' "It will NOT delete any data, but will modify migration history"
    Write-Host ""

    $confirm = Read-Host "Do you want to continue? (yes/no)"
    if ($confirm -ne 'yes') {
        Write-MigrationStatus 'info' "Operation cancelled"
        exit 0
    }

    # Run the fix
    Write-MigrationStatus 'info' "Running migration fix..."

    # Execute psql with the script
    $psqlResult = & psql $databaseUrl -f $tempSql 2>&1
    $exitCode = $LASTEXITCODE

    # Display output
    $psqlResult | ForEach-Object { Write-Host $_ }

    if ($exitCode -eq 0) {
        Write-MigrationStatus 'success' "Migration fix completed successfully"
        Write-MigrationStatus 'info' "Please restart your application now"
    } else {
        Write-MigrationStatus 'error' "Migration fix failed"
        exit 1
    }
} finally {
    # Clean up temp file
    if (Test-Path $tempSql) {
        Remove-Item $tempSql -Force -ErrorAction SilentlyContinue
    }
}
