-- Add frontier model costs for Anthropic and OpenAI models

-- Check table existence - this helps with giving better error messages
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ModelCosts') THEN
        RAISE EXCEPTION 'Table "ModelCosts" does not exist. Please make sure you are connected to the correct database.';
    END IF;
END $$;

-- First, make sure any existing records with the same model patterns are removed
-- This ensures a clean slate for our inserts
DELETE FROM "ModelCosts" WHERE "ModelIdPattern" LIKE 'anthropic/%' OR "ModelIdPattern" LIKE 'openai/%';

-- Anthropic Models - Claude 3 Family
INSERT INTO "ModelCosts" ("ModelIdPattern", "InputTokenCost", "OutputTokenCost", "EmbeddingTokenCost", "ImageCostPerImage", "CreatedAt", "UpdatedAt")
VALUES
  -- Claude 3 Opus
  ('anthropic/claude-3-opus-20240229', 0.0150000000, 0.0750000000, NULL, NULL, NOW(), NOW()),
  
  -- Claude 3 Sonnet
  ('anthropic/claude-3-sonnet-20240229', 0.0030000000, 0.0150000000, NULL, NULL, NOW(), NOW()),
  
  -- Claude 3 Haiku
  ('anthropic/claude-3-haiku-20240307', 0.0002500000, 0.0012500000, NULL, NULL, NOW(), NOW()),
  
  -- Add generic pattern for all Claude 3 models (for ease of use)
  ('anthropic/claude-3*', 0.0030000000, 0.0150000000, NULL, NULL, NOW(), NOW()),
  
  -- Claude 2.1
  ('anthropic/claude-2.1', 0.0080000000, 0.0240000000, NULL, NULL, NOW(), NOW()),
  
  -- Claude 2.0
  ('anthropic/claude-2.0', 0.0080000000, 0.0240000000, NULL, NULL, NOW(), NOW()),
  
  -- Claude Instant 1.2
  ('anthropic/claude-instant-1.2', 0.0008000000, 0.0024000000, NULL, NULL, NOW(), NOW());

-- OpenAI Models - GPT-4 Family
INSERT INTO "ModelCosts" ("ModelIdPattern", "InputTokenCost", "OutputTokenCost", "EmbeddingTokenCost", "ImageCostPerImage", "CreatedAt", "UpdatedAt")
VALUES
  -- GPT-4o
  ('openai/gpt-4o', 0.0050000000, 0.0150000000, NULL, NULL, NOW(), NOW()),
  
  -- GPT-4o Mini
  ('openai/gpt-4o-mini', 0.0005000000, 0.0015000000, NULL, NULL, NOW(), NOW()),
  
  -- GPT-4 Turbo
  ('openai/gpt-4-turbo', 0.0100000000, 0.0300000000, NULL, NULL, NOW(), NOW()),
  
  -- GPT-4 Turbo Preview
  ('openai/gpt-4-1106-preview', 0.0100000000, 0.0300000000, NULL, NULL, NOW(), NOW()),
  
  -- GPT-4 Latest Preview
  ('openai/gpt-4-0125-preview', 0.0100000000, 0.0300000000, NULL, NULL, NOW(), NOW()),
  
  -- GPT-4 Vision Preview
  ('openai/gpt-4-vision-preview', 0.0100000000, 0.0300000000, NULL, NULL, NOW(), NOW()),
  
  -- GPT-4 32k
  ('openai/gpt-4-32k', 0.0600000000, 0.1200000000, NULL, NULL, NOW(), NOW()),
  
  -- GPT-4 (original)
  ('openai/gpt-4', 0.0300000000, 0.0600000000, NULL, NULL, NOW(), NOW()),
  
  -- Add generic pattern for all GPT-4 models (for ease of use)
  ('openai/gpt-4*', 0.0100000000, 0.0300000000, NULL, NULL, NOW(), NOW());

-- OpenAI Models - GPT-3.5 Family
INSERT INTO "ModelCosts" ("ModelIdPattern", "InputTokenCost", "OutputTokenCost", "EmbeddingTokenCost", "ImageCostPerImage", "CreatedAt", "UpdatedAt")
VALUES
  -- GPT-3.5 Turbo
  ('openai/gpt-3.5-turbo', 0.0005000000, 0.0015000000, NULL, NULL, NOW(), NOW()),
  
  -- GPT-3.5 Turbo 16k
  ('openai/gpt-3.5-turbo-16k', 0.0010000000, 0.0020000000, NULL, NULL, NOW(), NOW()),
  
  -- Add generic pattern for all GPT-3.5 models (for ease of use)
  ('openai/gpt-3.5*', 0.0005000000, 0.0015000000, NULL, NULL, NOW(), NOW());

-- OpenAI - Embedding Models
INSERT INTO "ModelCosts" ("ModelIdPattern", "InputTokenCost", "OutputTokenCost", "EmbeddingTokenCost", "ImageCostPerImage", "CreatedAt", "UpdatedAt")
VALUES
  -- Text Embedding 3 Small
  ('openai/text-embedding-3-small', 0.0000200000, 0.0000000000, 0.0000200000, NULL, NOW(), NOW()),
  
  -- Text Embedding 3 Large
  ('openai/text-embedding-3-large', 0.0001300000, 0.0000000000, 0.0001300000, NULL, NOW(), NOW()),
  
  -- Text Embedding Ada 002
  ('openai/text-embedding-ada-002', 0.0001000000, 0.0000000000, 0.0001000000, NULL, NOW(), NOW());

-- OpenAI - Image Models (DALL-E)
INSERT INTO "ModelCosts" ("ModelIdPattern", "InputTokenCost", "OutputTokenCost", "EmbeddingTokenCost", "ImageCostPerImage", "CreatedAt", "UpdatedAt")
VALUES
  -- DALL-E 3
  ('openai/dall-e-3', 0.0000000000, 0.0000000000, NULL, 0.0400, NOW(), NOW()),
  
  -- DALL-E 2
  ('openai/dall-e-2', 0.0000000000, 0.0000000000, NULL, 0.0200, NOW(), NOW());