-- Backup script for production data
-- Run this BEFORE any migration fixes

-- Create backup schema
CREATE SCHEMA IF NOT EXISTS backup_before_migration;

-- Backup critical tables with data
CREATE TABLE backup_before_migration."VirtualKeys" AS SELECT * FROM "VirtualKeys";
CREATE TABLE backup_before_migration."BatchOperationHistory" AS SELECT * FROM "BatchOperationHistory";
CREATE TABLE backup_before_migration."MediaLifecycleRecords" AS SELECT * FROM "MediaLifecycleRecords";
CREATE TABLE backup_before_migration."RequestLogs" AS SELECT * FROM "RequestLogs";
CREATE TABLE backup_before_migration."ProviderCredentials" AS SELECT * FROM "ProviderCredentials";
CREATE TABLE backup_before_migration."GlobalSettings" AS SELECT * FROM "GlobalSettings";
CREATE TABLE backup_before_migration."ModelCosts" AS SELECT * FROM "ModelCosts";
CREATE TABLE backup_before_migration."AsyncTasks" AS SELECT * FROM "AsyncTasks";
CREATE TABLE backup_before_migration."MediaRecords" AS SELECT * FROM "MediaRecords";

-- Verify backup
SELECT 'VirtualKeys' as table_name, COUNT(*) as row_count FROM backup_before_migration."VirtualKeys"
UNION ALL
SELECT 'BatchOperationHistory', COUNT(*) FROM backup_before_migration."BatchOperationHistory"
UNION ALL
SELECT 'MediaLifecycleRecords', COUNT(*) FROM backup_before_migration."MediaLifecycleRecords"
UNION ALL
SELECT 'RequestLogs', COUNT(*) FROM backup_before_migration."RequestLogs";