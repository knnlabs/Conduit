-- Safe migration fix for production
-- This script safely handles the migration issue without data loss

-- Step 1: Check if migrations history table exists
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM information_schema.tables 
        WHERE table_schema = 'public' 
        AND table_name = '__EFMigrationsHistory'
    ) THEN
        -- Create the migrations history table
        CREATE TABLE "__EFMigrationsHistory" (
            "MigrationId" VARCHAR(300) NOT NULL,
            "ProductVersion" VARCHAR(32) NOT NULL,
            CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
        );
        RAISE NOTICE 'Created __EFMigrationsHistory table';
    END IF;
END $$;

-- Step 2: Check what tables already exist
DO $$
DECLARE
    v_tables_exist boolean := false;
    v_migration_exists boolean := false;
BEGIN
    -- Check if our tables exist
    SELECT EXISTS (
        SELECT FROM information_schema.tables 
        WHERE table_schema = 'public' 
        AND table_name IN ('BatchOperationHistory', 'MediaLifecycleRecords')
    ) INTO v_tables_exist;
    
    -- Check if migration is already recorded
    SELECT EXISTS (
        SELECT FROM "__EFMigrationsHistory" 
        WHERE "MigrationId" = '20250723043111_InitialCreate'
    ) INTO v_migration_exists;
    
    -- If tables exist but migration is not recorded, record it
    IF v_tables_exist AND NOT v_migration_exists THEN
        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") 
        VALUES ('20250723043111_InitialCreate', '9.0.0');
        RAISE NOTICE 'Marked InitialCreate migration as applied';
    ELSIF v_migration_exists THEN
        RAISE NOTICE 'Migration already marked as applied';
    ELSIF NOT v_tables_exist THEN
        RAISE NOTICE 'Tables do not exist - migration should run normally';
    END IF;
END $$;

-- Step 3: Verify the fix
SELECT 
    'Migration History' as check_type,
    COUNT(*) as count,
    STRING_AGG("MigrationId", ', ') as migrations
FROM "__EFMigrationsHistory"
GROUP BY check_type

UNION ALL

SELECT 
    'Existing Tables' as check_type,
    COUNT(*) as count,
    STRING_AGG(table_name, ', ') as migrations
FROM information_schema.tables 
WHERE table_schema = 'public' 
AND table_name IN (
    'BatchOperationHistory', 
    'MediaLifecycleRecords', 
    'VirtualKeys',
    'RequestLogs',
    'AsyncTasks',
    'MediaRecords'
)
GROUP BY check_type;