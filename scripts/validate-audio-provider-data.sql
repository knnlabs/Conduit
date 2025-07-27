-- Validation script for Audio Provider Type Migration
-- Run this BEFORE applying the migration to identify any unmapped providers

-- 1. Find all unique provider values in AudioCosts
SELECT 'AudioCosts' as "Table", 
       LOWER("Provider") as "NormalizedProvider", 
       COUNT(*) as "Count"
FROM "AudioCosts"
GROUP BY LOWER("Provider")
ORDER BY "Count" DESC;

-- 2. Find all unique provider values in AudioUsageLogs
SELECT 'AudioUsageLogs' as "Table", 
       LOWER("Provider") as "NormalizedProvider", 
       COUNT(*) as "Count"
FROM "AudioUsageLogs"
GROUP BY LOWER("Provider")
ORDER BY "Count" DESC;

-- 3. Identify providers that don't map to known enum values
WITH known_providers AS (
    SELECT unnest(ARRAY[
        'openai', 'anthropic', 'azureopenai', 'gemini', 'vertexai',
        'cohere', 'mistral', 'groq', 'ollama', 'replicate',
        'fireworks', 'bedrock', 'huggingface', 'sagemaker', 'openrouter',
        'openaicompatible', 'minimax', 'ultravox', 'elevenlabs', 'googlecloud',
        'cerebras', 'awstranscribe'
    ]) as provider
),
all_providers AS (
    SELECT DISTINCT LOWER("Provider") as provider FROM "AudioCosts"
    UNION
    SELECT DISTINCT LOWER("Provider") as provider FROM "AudioUsageLogs"
)
SELECT ap.provider as "UnmappedProvider"
FROM all_providers ap
LEFT JOIN known_providers kp ON ap.provider = kp.provider
WHERE kp.provider IS NULL;

-- 4. Summary statistics
SELECT 
    'Total Unique Providers' as "Metric",
    COUNT(DISTINCT LOWER("Provider")) as "AudioCosts",
    (SELECT COUNT(DISTINCT LOWER("Provider")) FROM "AudioUsageLogs") as "AudioUsageLogs"
FROM "AudioCosts";