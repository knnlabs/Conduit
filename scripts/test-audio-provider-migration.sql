-- Test script for Audio Provider Type Migration
-- Run this after applying the MigrateAudioToProviderTypeEnum migration

-- Test 1: Verify AudioCosts table structure
SELECT 
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_name = 'AudioCosts' 
  AND column_name = 'Provider';

-- Test 2: Verify AudioUsageLogs table structure
SELECT 
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_name = 'AudioUsageLogs' 
  AND column_name = 'Provider';

-- Test 3: Check Provider values are valid integers (1-22)
SELECT 'AudioCosts' as "Table", 
       "Provider", 
       COUNT(*) as "Count"
FROM "AudioCosts"
GROUP BY "Provider"
UNION ALL
SELECT 'AudioUsageLogs' as "Table", 
       "Provider", 
       COUNT(*) as "Count"
FROM "AudioUsageLogs"
GROUP BY "Provider"
ORDER BY "Table", "Provider";

-- Test 4: Verify no NULL providers
SELECT 
    (SELECT COUNT(*) FROM "AudioCosts" WHERE "Provider" IS NULL) as "AudioCosts_NULL_Count",
    (SELECT COUNT(*) FROM "AudioUsageLogs" WHERE "Provider" IS NULL) as "AudioUsageLogs_NULL_Count";

-- Test 5: Check for any providers outside valid range (1-22)
SELECT 'AudioCosts' as "Table", "Id", "Provider"
FROM "AudioCosts"
WHERE "Provider" < 1 OR "Provider" > 22
UNION ALL
SELECT 'AudioUsageLogs' as "Table", CAST("Id" AS INTEGER), "Provider"
FROM "AudioUsageLogs"
WHERE "Provider" < 1 OR "Provider" > 22;

-- Test 6: Sample data verification with provider names
SELECT 
    ac."Id",
    ac."Provider",
    CASE ac."Provider"
        WHEN 1 THEN 'OpenAI'
        WHEN 2 THEN 'Anthropic'
        WHEN 3 THEN 'AzureOpenAI'
        WHEN 4 THEN 'Gemini'
        WHEN 5 THEN 'VertexAI'
        WHEN 6 THEN 'Cohere'
        WHEN 7 THEN 'Mistral'
        WHEN 8 THEN 'Groq'
        WHEN 9 THEN 'Ollama'
        WHEN 10 THEN 'Replicate'
        WHEN 11 THEN 'Fireworks'
        WHEN 12 THEN 'Bedrock'
        WHEN 13 THEN 'HuggingFace'
        WHEN 14 THEN 'SageMaker'
        WHEN 15 THEN 'OpenRouter'
        WHEN 16 THEN 'OpenAICompatible'
        WHEN 17 THEN 'MiniMax'
        WHEN 18 THEN 'Ultravox'
        WHEN 19 THEN 'ElevenLabs'
        WHEN 20 THEN 'GoogleCloud'
        WHEN 21 THEN 'Cerebras'
        WHEN 22 THEN 'AWSTranscribe'
        ELSE 'Unknown'
    END as "ProviderName",
    ac."OperationType",
    ac."Model",
    ac."CostPerUnit"
FROM "AudioCosts" ac
LIMIT 10;

-- Test 7: Check constraints (Provider should not be null)
SELECT 
    tc.constraint_name,
    tc.table_name,
    kcu.column_name,
    tc.constraint_type
FROM information_schema.table_constraints tc
JOIN information_schema.key_column_usage kcu 
    ON tc.constraint_name = kcu.constraint_name
WHERE tc.table_name IN ('AudioCosts', 'AudioUsageLogs')
  AND kcu.column_name = 'Provider'
  AND tc.constraint_type = 'NOT NULL';

-- Test 8: Summary report
SELECT 
    'Migration Test Summary' as "Report",
    CASE 
        WHEN (
            SELECT COUNT(*) 
            FROM information_schema.columns 
            WHERE table_name IN ('AudioCosts', 'AudioUsageLogs') 
              AND column_name = 'Provider' 
              AND data_type = 'integer'
        ) = 2 
        THEN '✓ PASS: Provider columns are integer type'
        ELSE '✗ FAIL: Provider columns are not integer type'
    END as "Column_Type_Check",
    CASE 
        WHEN (
            SELECT COUNT(*) 
            FROM "AudioCosts" 
            WHERE "Provider" IS NULL
        ) + (
            SELECT COUNT(*) 
            FROM "AudioUsageLogs" 
            WHERE "Provider" IS NULL
        ) = 0
        THEN '✓ PASS: No NULL providers found'
        ELSE '✗ FAIL: NULL providers exist'
    END as "NULL_Check",
    CASE 
        WHEN (
            SELECT COUNT(*) 
            FROM "AudioCosts" 
            WHERE "Provider" < 1 OR "Provider" > 22
        ) + (
            SELECT COUNT(*) 
            FROM "AudioUsageLogs" 
            WHERE "Provider" < 1 OR "Provider" > 22
        ) = 0
        THEN '✓ PASS: All providers in valid range (1-22)'
        ELSE '✗ FAIL: Providers outside valid range found'
    END as "Range_Check";