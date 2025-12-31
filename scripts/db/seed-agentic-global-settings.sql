-- Seed script for Agentic Mode Global Settings
-- These settings control the default behavior and limits for agentic function calling loops
--
-- Usage:
-- From host: docker exec conduit-postgres-1 psql -U conduit -d conduitdb -f /path/to/seed-agentic-global-settings.sql
-- From container: psql -U conduit -d conduitdb -f /path/to/seed-agentic-global-settings.sql
--
-- Alternatively, create via Admin API:
-- POST /api/GlobalSettings
-- {
--   "key": "Agentic.MaxIterations",
--   "value": "5",
--   "description": "Maximum number of agentic loop iterations allowed per request"
-- }

-- Insert Agentic.MaxIterations if it doesn't exist
INSERT INTO "GlobalSettings" ("Key", "Value", "Description", "CreatedAt", "UpdatedAt")
SELECT
    'Agentic.MaxIterations',
    '5',
    'Maximum number of agentic loop iterations allowed per request. Valid range: 1-100. Default: 5.',
    NOW(),
    NOW()
WHERE NOT EXISTS (
    SELECT 1 FROM "GlobalSettings" WHERE "Key" = 'Agentic.MaxIterations'
);

-- Insert Agentic.MinIterations if it doesn't exist
INSERT INTO "GlobalSettings" ("Key", "Value", "Description", "CreatedAt", "UpdatedAt")
SELECT
    'Agentic.MinIterations',
    '1',
    'Minimum number of agentic iterations allowed per request. Valid range: 1-100. Default: 1.',
    NOW(),
    NOW()
WHERE NOT EXISTS (
    SELECT 1 FROM "GlobalSettings" WHERE "Key" = 'Agentic.MinIterations'
);

-- Insert Agentic.DefaultEnabled if it doesn't exist
INSERT INTO "GlobalSettings" ("Key", "Value", "Description", "CreatedAt", "UpdatedAt")
SELECT
    'Agentic.DefaultEnabled',
    'true',
    'Default state for agentic mode when not specified in request. Set to ''true'' or ''false''.',
    NOW(),
    NOW()
WHERE NOT EXISTS (
    SELECT 1 FROM "GlobalSettings" WHERE "Key" = 'Agentic.DefaultEnabled'
);

-- Display the inserted/existing settings
SELECT "Key", "Value", "Description", "CreatedAt", "UpdatedAt"
FROM "GlobalSettings"
WHERE "Key" IN ('Agentic.MaxIterations', 'Agentic.MinIterations', 'Agentic.DefaultEnabled')
ORDER BY "Key";
