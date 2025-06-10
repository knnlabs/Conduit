-- Initialize AutoLogin setting for development environments
-- This script adds the AutoLogin global setting to enable automatic login
-- when CONDUIT_MASTER_KEY environment variable is set

INSERT INTO "GlobalSettings" ("Key", "Value", "CreatedAt", "UpdatedAt")
VALUES ('AutoLogin', 'true', datetime('now'), datetime('now'))
ON CONFLICT("Key") DO UPDATE SET
    "Value" = excluded."Value",
    "UpdatedAt" = datetime('now');