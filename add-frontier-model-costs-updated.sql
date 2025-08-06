-- Updated Model Cost Configuration Script
-- This script creates model costs using the new ModelCost + ModelCostMapping architecture
-- instead of the deprecated ModelIdPattern approach

-- Check table existence
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ModelCosts') THEN
        RAISE EXCEPTION 'Table "ModelCosts" does not exist. Please make sure you are connected to the correct database.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ModelCostMappings') THEN
        RAISE EXCEPTION 'Table "ModelCostMappings" does not exist. Please make sure you are connected to the correct database.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ModelProviderMappings') THEN
        RAISE EXCEPTION 'Table "ModelProviderMappings" does not exist. Please make sure you are connected to the correct database.';
    END IF;
END $$;

-- Clean up existing frontier model costs (optional)
-- DELETE FROM "ModelCostMappings" WHERE "ModelCostId" IN (
--     SELECT "Id" FROM "ModelCosts" WHERE "CostName" LIKE '%Anthropic%' OR "CostName" LIKE '%OpenAI%'
-- );
-- DELETE FROM "ModelCosts" WHERE "CostName" LIKE '%Anthropic%' OR "CostName" LIKE '%OpenAI%';

-- Create Anthropic Model Costs
INSERT INTO "ModelCosts" (
    "CostName", 
    "InputTokenCost", 
    "OutputTokenCost", 
    "EmbeddingTokenCost", 
    "ImageCostPerImage",
    "ModelType",
    "IsActive",
    "Priority",
    "Description",
    "CreatedAt", 
    "UpdatedAt"
) VALUES 
-- Claude 3 Family
('Anthropic Claude 3 Opus', 0.0150000000, 0.0750000000, NULL, NULL, 'chat', true, 0, 'Claude 3 Opus - Most capable model', NOW(), NOW()),
('Anthropic Claude 3 Sonnet', 0.0030000000, 0.0150000000, NULL, NULL, 'chat', true, 0, 'Claude 3 Sonnet - Balanced performance', NOW(), NOW()),
('Anthropic Claude 3 Haiku', 0.0002500000, 0.0012500000, NULL, NULL, 'chat', true, 0, 'Claude 3 Haiku - Fastest model', NOW(), NOW()),

-- Claude 2 Family
('Anthropic Claude 2.1', 0.0080000000, 0.0240000000, NULL, NULL, 'chat', true, 0, 'Claude 2.1 - Previous generation', NOW(), NOW()),
('Anthropic Claude 2.0', 0.0080000000, 0.0240000000, NULL, NULL, 'chat', true, 0, 'Claude 2.0 - Previous generation', NOW(), NOW()),
('Anthropic Claude Instant', 0.0008000000, 0.0024000000, NULL, NULL, 'chat', true, 0, 'Claude Instant - Fast and affordable', NOW(), NOW());

-- Create OpenAI Model Costs
INSERT INTO "ModelCosts" (
    "CostName", 
    "InputTokenCost", 
    "OutputTokenCost", 
    "EmbeddingTokenCost", 
    "ImageCostPerImage",
    "ModelType",
    "IsActive",
    "Priority",
    "Description",
    "CreatedAt", 
    "UpdatedAt"
) VALUES 
-- GPT-4 Family
('OpenAI GPT-4o', 0.0050000000, 0.0150000000, NULL, NULL, 'chat', true, 0, 'GPT-4o - Latest multimodal model', NOW(), NOW()),
('OpenAI GPT-4o Mini', 0.0005000000, 0.0015000000, NULL, NULL, 'chat', true, 0, 'GPT-4o Mini - Affordable intelligence', NOW(), NOW()),
('OpenAI GPT-4 Turbo', 0.0100000000, 0.0300000000, NULL, NULL, 'chat', true, 0, 'GPT-4 Turbo - High performance', NOW(), NOW()),
('OpenAI GPT-4 Turbo Preview', 0.0100000000, 0.0300000000, NULL, NULL, 'chat', true, 0, 'GPT-4 Turbo Preview models', NOW(), NOW()),
('OpenAI GPT-4 Vision', 0.0100000000, 0.0300000000, NULL, NULL, 'chat', true, 0, 'GPT-4 Vision - Multimodal capabilities', NOW(), NOW()),
('OpenAI GPT-4 32K', 0.0600000000, 0.1200000000, NULL, NULL, 'chat', true, 0, 'GPT-4 32K - Extended context', NOW(), NOW()),
('OpenAI GPT-4', 0.0300000000, 0.0600000000, NULL, NULL, 'chat', true, 0, 'GPT-4 - Original model', NOW(), NOW()),

-- GPT-3.5 Family
('OpenAI GPT-3.5 Turbo', 0.0005000000, 0.0015000000, NULL, NULL, 'chat', true, 0, 'GPT-3.5 Turbo - Fast and affordable', NOW(), NOW()),
('OpenAI GPT-3.5 Turbo 16K', 0.0010000000, 0.0020000000, NULL, NULL, 'chat', true, 0, 'GPT-3.5 Turbo 16K - Extended context', NOW(), NOW());

-- Create OpenAI Embedding Model Costs
INSERT INTO "ModelCosts" (
    "CostName", 
    "InputTokenCost", 
    "OutputTokenCost", 
    "EmbeddingTokenCost", 
    "ImageCostPerImage",
    "ModelType",
    "IsActive",
    "Priority",
    "Description",
    "CreatedAt", 
    "UpdatedAt"
) VALUES 
('OpenAI Text Embedding 3 Small', 0.0000200000, 0.0000000000, 0.0000200000, NULL, 'embedding', true, 0, 'Text Embedding 3 Small - Most affordable', NOW(), NOW()),
('OpenAI Text Embedding 3 Large', 0.0001300000, 0.0000000000, 0.0001300000, NULL, 'embedding', true, 0, 'Text Embedding 3 Large - Highest performance', NOW(), NOW()),
('OpenAI Text Embedding Ada 002', 0.0001000000, 0.0000000000, 0.0001000000, NULL, 'embedding', true, 0, 'Text Embedding Ada 002 - Legacy model', NOW(), NOW());

-- Create OpenAI Image Generation Model Costs
INSERT INTO "ModelCosts" (
    "CostName", 
    "InputTokenCost", 
    "OutputTokenCost", 
    "EmbeddingTokenCost", 
    "ImageCostPerImage",
    "ModelType",
    "IsActive",
    "Priority",
    "Description",
    "CreatedAt", 
    "UpdatedAt"
) VALUES 
('OpenAI DALL-E 3', 0.0000000000, 0.0000000000, NULL, 0.0400, 'image', true, 0, 'DALL-E 3 - Latest image generation', NOW(), NOW()),
('OpenAI DALL-E 2', 0.0000000000, 0.0000000000, NULL, 0.0200, 'image', true, 0, 'DALL-E 2 - Previous generation', NOW(), NOW());

-- Display created costs
SELECT 
    "Id",
    "CostName",
    "ModelType",
    "InputTokenCost" * 1000 as "Input Cost per 1K tokens",
    "OutputTokenCost" * 1000 as "Output Cost per 1K tokens",
    CASE 
        WHEN "EmbeddingTokenCost" IS NOT NULL THEN "EmbeddingTokenCost" * 1000
        ELSE NULL 
    END as "Embedding Cost per 1K tokens",
    "ImageCostPerImage" as "Cost per Image",
    "Description"
FROM "ModelCosts" 
WHERE "CostName" LIKE '%Anthropic%' OR "CostName" LIKE '%OpenAI%'
ORDER BY "CostName";

-- Instructions for linking to models:
-- 
-- To link these costs to specific models, you need to:
-- 1. Find the ModelProviderMapping IDs for your models:
--    SELECT "Id", "ModelAlias", "ProviderModelId" FROM "ModelProviderMappings" 
--    WHERE "ModelAlias" LIKE 'anthropic/%' OR "ModelAlias" LIKE 'openai/%';
--
-- 2. Create ModelCostMappings to link costs to models:
--    INSERT INTO "ModelCostMappings" ("ModelCostId", "ModelProviderMappingId", "IsActive", "CreatedAt")
--    VALUES 
--    ((SELECT "Id" FROM "ModelCosts" WHERE "CostName" = 'OpenAI GPT-4o'), 
--     (SELECT "Id" FROM "ModelProviderMappings" WHERE "ModelAlias" = 'openai/gpt-4o'), 
--     true, NOW());
--
-- 3. Repeat for all model/cost combinations you want to configure.
--
-- Alternatively, use the Admin API or Admin UI to manage these mappings.
