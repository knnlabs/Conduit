-- Script to populate initial ProviderKeyCredentials from existing ProviderCredential ApiKeys
-- This should be run after the AddProviderKeyCredentials migration

-- Insert a primary key credential for each provider that has an ApiKey
INSERT INTO "ProviderKeyCredentials" (
    "ProviderCredentialId",
    "ProviderAccountGroup",
    "ApiKey",
    "BaseUrl",
    "ApiVersion",
    "IsPrimary",
    "IsEnabled",
    "CreatedAt",
    "UpdatedAt"
)
SELECT 
    pc."Id",
    0, -- Default account group
    pc."ApiKey",
    pc."BaseUrl",
    pc."ApiVersion",
    true, -- Set as primary
    pc."IsEnabled",
    pc."CreatedAt",
    pc."UpdatedAt"
FROM "ProviderCredentials" pc
WHERE pc."ApiKey" IS NOT NULL 
  AND pc."ApiKey" != ''
  AND NOT EXISTS (
      -- Only insert if no keys exist for this provider yet
      SELECT 1 
      FROM "ProviderKeyCredentials" pkc 
      WHERE pkc."ProviderCredentialId" = pc."Id"
  );

-- Update ProviderType based on ProviderName (case-insensitive)
UPDATE "ProviderCredentials" 
SET "ProviderType" = CASE 
    WHEN LOWER("ProviderName") = 'openai' THEN 1
    WHEN LOWER("ProviderName") = 'anthropic' THEN 2
    WHEN LOWER("ProviderName") IN ('azure-openai', 'azureopenai') THEN 3
    WHEN LOWER("ProviderName") IN ('gemini', 'google') THEN 4
    WHEN LOWER("ProviderName") IN ('vertexai', 'vertex-ai') THEN 5
    WHEN LOWER("ProviderName") = 'cohere' THEN 6
    WHEN LOWER("ProviderName") IN ('mistral', 'mistralai') THEN 7
    WHEN LOWER("ProviderName") = 'groq' THEN 8
    WHEN LOWER("ProviderName") = 'ollama' THEN 9
    WHEN LOWER("ProviderName") = 'replicate' THEN 10
    WHEN LOWER("ProviderName") IN ('fireworks', 'fireworksai') THEN 11
    WHEN LOWER("ProviderName") = 'bedrock' THEN 12
    WHEN LOWER("ProviderName") IN ('huggingface', 'hugging-face') THEN 13
    WHEN LOWER("ProviderName") = 'sagemaker' THEN 14
    WHEN LOWER("ProviderName") IN ('openrouter', 'open-router') THEN 15
    WHEN LOWER("ProviderName") IN ('openai-compatible', 'openaicompatible') THEN 16
    WHEN LOWER("ProviderName") = 'minimax' THEN 17
    WHEN LOWER("ProviderName") = 'ultravox' THEN 18
    WHEN LOWER("ProviderName") IN ('elevenlabs', 'eleven-labs') THEN 19
    WHEN LOWER("ProviderName") IN ('googlecloud', 'google-cloud', 'gcp') THEN 20
    ELSE 1 -- Default to OpenAI if unknown
END
WHERE "ProviderType" = 0; -- Only update if not already set