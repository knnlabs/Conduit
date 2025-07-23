#!/bin/bash
set -e

# Script: fix-production-migrations.sh
# Purpose: Fix production database migration issues
# WARNING: This script should be run with extreme caution in production

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

print_status() {
    local status=$1
    local message=$2
    case $status in
        "error")   echo -e "${RED}✗ ERROR:${NC} $message" >&2 ;;
        "success") echo -e "${GREEN}✓${NC} $message" ;;
        "warning") echo -e "${YELLOW}⚠${NC} $message" ;;
        "info")    echo -e "${BLUE}ℹ${NC} $message" ;;
    esac
}

# Validate environment
if [ -z "$DATABASE_URL" ]; then
    print_status "error" "DATABASE_URL environment variable is not set"
    exit 1
fi

# Extract database name from DATABASE_URL
DB_NAME=$(echo $DATABASE_URL | sed -n 's/.*\/\([^?]*\).*/\1/p')
print_status "info" "Working with database: $DB_NAME"

# Create a temporary SQL file
TEMP_SQL=$(mktemp /tmp/fix-migrations-XXXXXX.sql)
trap "rm -f $TEMP_SQL" EXIT

cat > $TEMP_SQL << 'EOF'
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
EOF

# Show what we're about to do
print_status "warning" "This script will fix migration issues in your production database"
print_status "warning" "It will NOT delete any data, but will modify migration history"
echo ""
read -p "Do you want to continue? (yes/no): " confirm

if [ "$confirm" != "yes" ]; then
    print_status "info" "Operation cancelled"
    exit 0
fi

# Run the fix
print_status "info" "Running migration fix..."
psql $DATABASE_URL -f $TEMP_SQL

if [ $? -eq 0 ]; then
    print_status "success" "Migration fix completed successfully"
    print_status "info" "Please restart your application now"
else
    print_status "error" "Migration fix failed"
    exit 1
fi