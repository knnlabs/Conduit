-- Create ProviderHealthConfigurations table
CREATE TABLE IF NOT EXISTS "ProviderHealthConfigurations" (
    "Id" SERIAL PRIMARY KEY,
    "ProviderName" VARCHAR(100) NOT NULL,
    "MonitoringEnabled" BOOLEAN NOT NULL DEFAULT TRUE,
    "NotificationsEnabled" BOOLEAN NOT NULL DEFAULT TRUE,
    "CheckIntervalMinutes" INTEGER NOT NULL DEFAULT 5,
    "TimeoutSeconds" INTEGER NOT NULL DEFAULT 30,
    "ConsecutiveFailuresThreshold" INTEGER NOT NULL DEFAULT 3,
    "CustomEndpointUrl" VARCHAR(500) NULL,
    "LastCheckedUtc" TIMESTAMP NULL
);

-- Create unique index on ProviderName
CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProviderHealthConfigurations_ProviderName" ON "ProviderHealthConfigurations" ("ProviderName");

-- Create ProviderHealthRecords table
CREATE TABLE IF NOT EXISTS "ProviderHealthRecords" (
    "Id" SERIAL PRIMARY KEY,
    "ProviderName" VARCHAR(100) NOT NULL,
    "TimestampUtc" TIMESTAMP NOT NULL,
    "IsOnline" BOOLEAN NOT NULL,
    "ResponseTimeMs" DOUBLE PRECISION NOT NULL,
    "StatusMessage" VARCHAR(1000) NULL,
    "ErrorCategory" VARCHAR(100) NULL,
    "ErrorDetails" VARCHAR(2000) NULL,
    "EndpointUrl" VARCHAR(500) NULL
);

-- Create index on ProviderName and TimestampUtc
CREATE INDEX IF NOT EXISTS "IX_ProviderHealthRecords_ProviderName_TimestampUtc" ON "ProviderHealthRecords" ("ProviderName", "TimestampUtc");