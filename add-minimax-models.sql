-- Add MiniMax provider credentials (example - replace with actual API key)
INSERT INTO ProviderCredentials (ProviderName, ApiKey, BaseUrl, ApiVersion, IsEnabled, CreatedAt, ModifiedAt)
SELECT 'minimax', 'YOUR_MINIMAX_API_KEY', 'https://api.minimax.chat/v1', 'v1', 1, datetime('now'), datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM ProviderCredentials WHERE ProviderName = 'minimax');

-- Add MiniMax image generation model mapping
INSERT INTO ModelProviderMappings (
    ModelAlias, 
    ProviderCredentialId, 
    ProviderModelName,
    SupportsVision,
    TokenizerType,
    IsDefault,
    DefaultCapabilityType,
    CreatedAt,
    ModifiedAt
)
SELECT 
    'minimax-image',
    pc.Id,
    'image-01',
    0,  -- Image generation, not vision input
    NULL,
    0,
    NULL,
    datetime('now'),
    datetime('now')
FROM ProviderCredentials pc
WHERE pc.ProviderName = 'minimax'
AND NOT EXISTS (
    SELECT 1 FROM ModelProviderMappings m 
    WHERE m.ModelAlias = 'minimax-image'
);

-- Add MiniMax chat model with vision support
INSERT INTO ModelProviderMappings (
    ModelAlias, 
    ProviderCredentialId, 
    ProviderModelName,
    SupportsVision,
    TokenizerType,
    IsDefault,
    DefaultCapabilityType,
    CreatedAt,
    ModifiedAt
)
SELECT 
    'minimax-chat',
    pc.Id,
    'abab6.5-chat',
    1,  -- Supports vision
    'minimax',
    0,
    NULL,
    datetime('now'),
    datetime('now')
FROM ProviderCredentials pc
WHERE pc.ProviderName = 'minimax'
AND NOT EXISTS (
    SELECT 1 FROM ModelProviderMappings m 
    WHERE m.ModelAlias = 'minimax-chat'
);

-- Add model costs for MiniMax models
-- Image generation cost (estimated)
INSERT INTO ModelCosts (
    ModelIdentifier,
    ProviderName,
    InputCostPerMillion,
    OutputCostPerMillion,
    ImageGenerationCost,
    EffectiveDate,
    CreatedAt,
    ModifiedAt
)
SELECT
    'image-01',
    'minimax',
    0.0,  -- No input cost for image generation
    0.0,  -- No output cost for image generation  
    0.02, -- $0.02 per image (estimated)
    date('now'),
    datetime('now'),
    datetime('now')
WHERE NOT EXISTS (
    SELECT 1 FROM ModelCosts 
    WHERE ModelIdentifier = 'image-01' AND ProviderName = 'minimax'
);

-- Chat model costs
INSERT INTO ModelCosts (
    ModelIdentifier,
    ProviderName,
    InputCostPerMillion,
    OutputCostPerMillion,
    EffectiveDate,
    CreatedAt,
    ModifiedAt
)
SELECT
    'abab6.5-chat',
    'minimax',
    15.0,  -- $15 per million input tokens (estimated)
    15.0,  -- $15 per million output tokens (estimated)
    date('now'),
    datetime('now'),
    datetime('now')
WHERE NOT EXISTS (
    SELECT 1 FROM ModelCosts 
    WHERE ModelIdentifier = 'abab6.5-chat' AND ProviderName = 'minimax'
);

-- Note: Replace 'YOUR_MINIMAX_API_KEY' with actual API key before running
-- To apply these changes:
-- 1. Update the API key in the first INSERT statement
-- 2. Run: sqlite3 conduit.db < add-minimax-models.sql