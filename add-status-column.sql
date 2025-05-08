-- Add Status column to ProviderHealthRecords table
ALTER TABLE "ProviderHealthRecords" ADD COLUMN "Status" INTEGER NOT NULL DEFAULT 0;

-- Update Status based on IsOnline for existing records
UPDATE "ProviderHealthRecords" 
SET "Status" = CASE WHEN "IsOnline" = true THEN 0 ELSE 1 END;