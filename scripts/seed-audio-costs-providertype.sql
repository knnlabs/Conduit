-- Seed data for AudioCosts table using ProviderType enum values
-- This script populates audio cost configurations for common providers
-- Run after the MigrateAudioToProviderTypeEnum migration

-- Clear existing audio costs (optional - remove if you want to keep existing data)
-- DELETE FROM "AudioCosts";

-- OpenAI (1) - Whisper transcription
INSERT INTO "AudioCosts" ("Provider", "OperationType", "Model", "CostUnit", "CostPerUnit", "MinimumCharge", "IsActive", "EffectiveFrom", "CreatedAt", "UpdatedAt")
VALUES (1, 'transcription', 'whisper-1', 'minute', 0.006, NULL, true, '2024-01-01', NOW(), NOW());

-- OpenAI (1) - TTS
INSERT INTO "AudioCosts" ("Provider", "OperationType", "Model", "CostUnit", "CostPerUnit", "MinimumCharge", "IsActive", "EffectiveFrom", "CreatedAt", "UpdatedAt")
VALUES (1, 'tts', 'tts-1', 'character', 0.000015, NULL, true, '2024-01-01', NOW(), NOW());

INSERT INTO "AudioCosts" ("Provider", "OperationType", "Model", "CostUnit", "CostPerUnit", "MinimumCharge", "IsActive", "EffectiveFrom", "CreatedAt", "UpdatedAt")
VALUES (1, 'tts', 'tts-1-hd', 'character', 0.00003, NULL, true, '2024-01-01', NOW(), NOW());

-- OpenAI (1) - Realtime
INSERT INTO "AudioCosts" ("Provider", "OperationType", "Model", "CostUnit", "CostPerUnit", "MinimumCharge", "IsActive", "EffectiveFrom", "CreatedAt", "UpdatedAt")
VALUES (1, 'realtime', 'gpt-4o-realtime-preview', 'minute', 0.06, NULL, true, '2024-10-01', NOW(), NOW());

-- Anthropic (2) - No audio services currently

-- ElevenLabs (19) - TTS
INSERT INTO "AudioCosts" ("Provider", "OperationType", "Model", "CostUnit", "CostPerUnit", "MinimumCharge", "IsActive", "EffectiveFrom", "CreatedAt", "UpdatedAt")
VALUES (19, 'tts', 'eleven_monolingual_v1', 'character', 0.00018, NULL, true, '2024-01-01', NOW(), NOW());

INSERT INTO "AudioCosts" ("Provider", "OperationType", "Model", "CostUnit", "CostPerUnit", "MinimumCharge", "IsActive", "EffectiveFrom", "CreatedAt", "UpdatedAt")
VALUES (19, 'tts', 'eleven_multilingual_v2', 'character', 0.00027, NULL, true, '2024-01-01', NOW(), NOW());

INSERT INTO "AudioCosts" ("Provider", "OperationType", "Model", "CostUnit", "CostPerUnit", "MinimumCharge", "IsActive", "EffectiveFrom", "CreatedAt", "UpdatedAt")
VALUES (19, 'tts', 'eleven_turbo_v2', 'character', 0.00018, NULL, true, '2024-01-01', NOW(), NOW());

-- GoogleCloud (20) - Speech-to-Text
INSERT INTO "AudioCosts" ("Provider", "OperationType", "Model", "CostUnit", "CostPerUnit", "MinimumCharge", "IsActive", "EffectiveFrom", "CreatedAt", "UpdatedAt")
VALUES (20, 'transcription', 'default', 'minute', 0.016, NULL, true, '2024-01-01', NOW(), NOW());

INSERT INTO "AudioCosts" ("Provider", "OperationType", "Model", "CostUnit", "CostPerUnit", "MinimumCharge", "IsActive", "EffectiveFrom", "CreatedAt", "UpdatedAt")
VALUES (20, 'transcription', 'enhanced', 'minute', 0.024, NULL, true, '2024-01-01', NOW(), NOW());

-- GoogleCloud (20) - Text-to-Speech
INSERT INTO "AudioCosts" ("Provider", "OperationType", "Model", "CostUnit", "CostPerUnit", "MinimumCharge", "IsActive", "EffectiveFrom", "CreatedAt", "UpdatedAt")
VALUES (20, 'tts', 'standard', 'character', 0.000004, NULL, true, '2024-01-01', NOW(), NOW());

INSERT INTO "AudioCosts" ("Provider", "OperationType", "Model", "CostUnit", "CostPerUnit", "MinimumCharge", "IsActive", "EffectiveFrom", "CreatedAt", "UpdatedAt")
VALUES (20, 'tts', 'wavenet', 'character', 0.000016, NULL, true, '2024-01-01', NOW(), NOW());

INSERT INTO "AudioCosts" ("Provider", "OperationType", "Model", "CostUnit", "CostPerUnit", "MinimumCharge", "IsActive", "EffectiveFrom", "CreatedAt", "UpdatedAt")
VALUES (20, 'tts', 'neural2', 'character', 0.000016, NULL, true, '2024-01-01', NOW(), NOW());

-- AWS Transcribe (22) - Transcription
INSERT INTO "AudioCosts" ("Provider", "OperationType", "Model", "CostUnit", "CostPerUnit", "MinimumCharge", "IsActive", "EffectiveFrom", "CreatedAt", "UpdatedAt")
VALUES (22, 'transcription', 'standard', 'second', 0.00040, NULL, true, '2024-01-01', NOW(), NOW());

INSERT INTO "AudioCosts" ("Provider", "OperationType", "Model", "CostUnit", "CostPerUnit", "MinimumCharge", "IsActive", "EffectiveFrom", "CreatedAt", "UpdatedAt")
VALUES (22, 'transcription', 'medical', 'second', 0.00075, NULL, true, '2024-01-01', NOW(), NOW());

-- Groq (8) - Whisper transcription (often free tier)
INSERT INTO "AudioCosts" ("Provider", "OperationType", "Model", "CostUnit", "CostPerUnit", "MinimumCharge", "IsActive", "EffectiveFrom", "CreatedAt", "UpdatedAt")
VALUES (8, 'transcription', 'whisper-large-v3', 'minute', 0.0, NULL, true, '2024-01-01', NOW(), NOW());

-- Verify the seeded data
SELECT 
    "Provider",
    CASE "Provider"
        WHEN 1 THEN 'OpenAI'
        WHEN 2 THEN 'Anthropic'
        WHEN 8 THEN 'Groq'
        WHEN 19 THEN 'ElevenLabs'
        WHEN 20 THEN 'GoogleCloud'
        WHEN 22 THEN 'AWSTranscribe'
        ELSE 'Unknown'
    END as "ProviderName",
    "OperationType",
    "Model",
    "CostUnit",
    "CostPerUnit",
    "IsActive"
FROM "AudioCosts"
ORDER BY "Provider", "OperationType", "Model";