-- Add image generation support to existing OpenAI DALL-E models
UPDATE ModelProviderMappings
SET SupportsImageGeneration = 1,
    UpdatedAt = datetime('now')
WHERE ModelAlias IN ('dall-e-2', 'dall-e-3')
AND EXISTS (
    SELECT 1 FROM ProviderCredentials pc 
    WHERE pc.Id = ModelProviderMappings.ProviderCredentialId 
    AND pc.ProviderName = 'openai'
);

-- Add DALL-E models if they don't exist
INSERT INTO ModelProviderMappings (
    ModelAlias, 
    ProviderCredentialId, 
    ProviderModelName,
    SupportsVision,
    SupportsImageGeneration,
    TokenizerType,
    IsDefault,
    DefaultCapabilityType,
    CreatedAt,
    UpdatedAt
)
SELECT 
    'dall-e-3',
    pc.Id,
    'dall-e-3',
    0,  -- Not a vision model
    1,  -- Supports image generation
    NULL,
    0,
    NULL,
    datetime('now'),
    datetime('now')
FROM ProviderCredentials pc
WHERE pc.ProviderName = 'openai'
AND NOT EXISTS (
    SELECT 1 FROM ModelProviderMappings m 
    WHERE m.ModelAlias = 'dall-e-3'
);

INSERT INTO ModelProviderMappings (
    ModelAlias, 
    ProviderCredentialId, 
    ProviderModelName,
    SupportsVision,
    SupportsImageGeneration,
    TokenizerType,
    IsDefault,
    DefaultCapabilityType,
    CreatedAt,
    UpdatedAt
)
SELECT 
    'dall-e-2',
    pc.Id,
    'dall-e-2',
    0,  -- Not a vision model
    1,  -- Supports image generation
    NULL,
    0,
    NULL,
    datetime('now'),
    datetime('now')
FROM ProviderCredentials pc
WHERE pc.ProviderName = 'openai'
AND NOT EXISTS (
    SELECT 1 FROM ModelProviderMappings m 
    WHERE m.ModelAlias = 'dall-e-2'
);

-- Note: To apply these changes:
-- 1. Run: sqlite3 conduit.db < add-image-generation-models.sql
-- 2. Or for PostgreSQL: psql -U username -d conduit < add-image-generation-models.sql