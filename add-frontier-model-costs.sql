-- Add frontier model costs for Anthropic and OpenAI models
-- All costs are in USD per million tokens (industry standard)

-- Check table existence - this helps with giving better error messages
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ModelCosts') THEN
        RAISE EXCEPTION 'Table "ModelCosts" does not exist. Please make sure you are connected to the correct database.';
    END IF;
END $$;

-- First, make sure any existing records with the same names are removed
-- This ensures a clean slate for our inserts
DELETE FROM "ModelCosts" WHERE "CostName" LIKE 'Claude%' OR "CostName" LIKE 'GPT%' OR "CostName" LIKE 'Text Embedding%' OR "CostName" LIKE 'DALL-E%';

-- Anthropic Models - Claude 3 Family
-- Costs are per million tokens
INSERT INTO "ModelCosts" (
    "CostName", 
    "InputCostPerMillionTokens", 
    "OutputCostPerMillionTokens", 
    "EmbeddingCostPerMillionTokens", 
    "ImageCostPerImage", 
    "ModelType",
    "Priority",
    "IsActive",
    "CreatedAt", 
    "UpdatedAt",
    "EffectiveDate"
)
VALUES
  -- Claude 3 Opus: $15/M input, $75/M output
  ('Claude 3 Opus', 15000.00, 75000.00, NULL, NULL, 'chat', 10, true, NOW(), NOW(), NOW()),
  
  -- Claude 3 Sonnet: $3/M input, $15/M output
  ('Claude 3 Sonnet', 3000.00, 15000.00, NULL, NULL, 'chat', 10, true, NOW(), NOW(), NOW()),
  
  -- Claude 3 Haiku: $0.25/M input, $1.25/M output
  ('Claude 3 Haiku', 250.00, 1250.00, NULL, NULL, 'chat', 10, true, NOW(), NOW(), NOW()),
  
  -- Claude 2.1: $8/M input, $24/M output
  ('Claude 2.1', 8000.00, 24000.00, NULL, NULL, 'chat', 5, true, NOW(), NOW(), NOW()),
  
  -- Claude 2.0: $8/M input, $24/M output
  ('Claude 2.0', 8000.00, 24000.00, NULL, NULL, 'chat', 5, true, NOW(), NOW(), NOW()),
  
  -- Claude Instant 1.2: $0.80/M input, $2.40/M output
  ('Claude Instant 1.2', 800.00, 2400.00, NULL, NULL, 'chat', 5, true, NOW(), NOW(), NOW());

-- OpenAI Models - GPT-4 Family
-- Costs are per million tokens
INSERT INTO "ModelCosts" (
    "CostName", 
    "InputCostPerMillionTokens", 
    "OutputCostPerMillionTokens", 
    "EmbeddingCostPerMillionTokens", 
    "ImageCostPerImage", 
    "ModelType",
    "Priority",
    "IsActive",
    "CreatedAt", 
    "UpdatedAt",
    "EffectiveDate"
)
VALUES
  -- GPT-4o: $5/M input, $15/M output
  ('GPT-4o', 5000.00, 15000.00, NULL, NULL, 'chat', 10, true, NOW(), NOW(), NOW()),
  
  -- GPT-4o Mini: $0.50/M input, $1.50/M output (corrected from 500/1500)
  ('GPT-4o Mini', 500.00, 1500.00, NULL, NULL, 'chat', 10, true, NOW(), NOW(), NOW()),
  
  -- GPT-4 Turbo: $10/M input, $30/M output
  ('GPT-4 Turbo', 10000.00, 30000.00, NULL, NULL, 'chat', 10, true, NOW(), NOW(), NOW()),
  
  -- GPT-4 Turbo Preview: $10/M input, $30/M output
  ('GPT-4 1106 Preview', 10000.00, 30000.00, NULL, NULL, 'chat', 8, true, NOW(), NOW(), NOW()),
  
  -- GPT-4 Latest Preview: $10/M input, $30/M output
  ('GPT-4 0125 Preview', 10000.00, 30000.00, NULL, NULL, 'chat', 8, true, NOW(), NOW(), NOW()),
  
  -- GPT-4 Vision Preview: $10/M input, $30/M output
  ('GPT-4 Vision Preview', 10000.00, 30000.00, NULL, NULL, 'chat', 8, true, NOW(), NOW(), NOW()),
  
  -- GPT-4 32k: $60/M input, $120/M output
  ('GPT-4 32k', 60000.00, 120000.00, NULL, NULL, 'chat', 7, true, NOW(), NOW(), NOW()),
  
  -- GPT-4 (original): $30/M input, $60/M output
  ('GPT-4', 30000.00, 60000.00, NULL, NULL, 'chat', 7, true, NOW(), NOW(), NOW());

-- OpenAI Models - GPT-3.5 Family
-- Costs are per million tokens
INSERT INTO "ModelCosts" (
    "CostName", 
    "InputCostPerMillionTokens", 
    "OutputCostPerMillionTokens", 
    "EmbeddingCostPerMillionTokens", 
    "ImageCostPerImage", 
    "ModelType",
    "Priority",
    "IsActive",
    "CreatedAt", 
    "UpdatedAt",
    "EffectiveDate"
)
VALUES
  -- GPT-3.5 Turbo: $0.50/M input, $1.50/M output
  ('GPT-3.5 Turbo', 500.00, 1500.00, NULL, NULL, 'chat', 5, true, NOW(), NOW(), NOW()),
  
  -- GPT-3.5 Turbo 16k: $1.00/M input, $2.00/M output
  ('GPT-3.5 Turbo 16k', 1000.00, 2000.00, NULL, NULL, 'chat', 5, true, NOW(), NOW(), NOW());

-- OpenAI - Embedding Models
-- Costs are per million tokens
INSERT INTO "ModelCosts" (
    "CostName", 
    "InputCostPerMillionTokens", 
    "OutputCostPerMillionTokens", 
    "EmbeddingCostPerMillionTokens", 
    "ImageCostPerImage", 
    "ModelType",
    "Priority",
    "IsActive",
    "CreatedAt", 
    "UpdatedAt",
    "EffectiveDate"
)
VALUES
  -- Text Embedding 3 Small: $0.02/M tokens
  ('Text Embedding 3 Small', 20.00, 0.00, 20.00, NULL, 'embedding', 10, true, NOW(), NOW(), NOW()),
  
  -- Text Embedding 3 Large: $0.13/M tokens  
  ('Text Embedding 3 Large', 130.00, 0.00, 130.00, NULL, 'embedding', 10, true, NOW(), NOW(), NOW()),
  
  -- Text Embedding Ada 002: $0.10/M tokens
  ('Text Embedding Ada 002', 100.00, 0.00, 100.00, NULL, 'embedding', 5, true, NOW(), NOW(), NOW());

-- OpenAI - Image Models (DALL-E)
-- Cost per image
INSERT INTO "ModelCosts" (
    "CostName", 
    "InputCostPerMillionTokens", 
    "OutputCostPerMillionTokens", 
    "EmbeddingCostPerMillionTokens", 
    "ImageCostPerImage", 
    "ModelType",
    "Priority",
    "IsActive",
    "CreatedAt", 
    "UpdatedAt",
    "EffectiveDate"
)
VALUES
  -- DALL-E 3: $0.04 per image
  ('DALL-E 3', 0.00, 0.00, NULL, 0.0400, 'image', 10, true, NOW(), NOW(), NOW()),
  
  -- DALL-E 2: $0.02 per image
  ('DALL-E 2', 0.00, 0.00, NULL, 0.0200, 'image', 5, true, NOW(), NOW(), NOW());

-- Display inserted records for verification
SELECT 
    "CostName",
    "ModelType",
    "InputCostPerMillionTokens" as "Input $/M",
    "OutputCostPerMillionTokens" as "Output $/M",
    "EmbeddingCostPerMillionTokens" as "Embedding $/M",
    "ImageCostPerImage" as "Image $"
FROM "ModelCosts"
ORDER BY "ModelType", "Priority" DESC, "CostName";