-- Seed Model Capabilities Data
-- This script populates the ModelProviderMapping table with capability information
-- for all supported models, replacing hardcoded values in the codebase

-- Note: This script assumes you already have ProviderCredential entries for each provider.
-- You may need to adjust the ProviderCredentialId values based on your actual data.

-- Helper: Get or create provider credential IDs (you'll need to adjust these based on your actual data)
-- For SQLite, use: SELECT Id FROM ProviderCredential WHERE Provider = 'openai' LIMIT 1;
-- For PostgreSQL, you can use variables or CTEs

-- OpenAI Models
-- Vision-capable models
UPDATE ModelProviderMapping 
SET SupportsVision = 1, TokenizerType = 'cl100k_base'
WHERE ModelAlias IN ('gpt-4-vision-preview', 'gpt-4-turbo', 'gpt-4-turbo-preview', 'gpt-4v')
  AND ProviderCredentialId IN (SELECT Id FROM ProviderCredential WHERE Provider = 'openai');

UPDATE ModelProviderMapping 
SET SupportsVision = 1, TokenizerType = 'o200k_base'
WHERE ModelAlias IN ('gpt-4o', 'gpt-4o-mini')
  AND ProviderCredentialId IN (SELECT Id FROM ProviderCredential WHERE Provider = 'openai');

-- Chat models (non-vision)
UPDATE ModelProviderMapping 
SET SupportsVision = 0, TokenizerType = 'cl100k_base'
WHERE ModelAlias IN ('gpt-3.5-turbo', 'gpt-4')
  AND ProviderCredentialId IN (SELECT Id FROM ProviderCredential WHERE Provider = 'openai');

-- Audio transcription model
UPDATE ModelProviderMapping 
SET SupportsAudioTranscription = 1,
    SupportedLanguages = '["en", "zh", "de", "es", "ru", "ko", "fr", "ja", "pt", "tr", "pl", "ca", "nl", "ar", "sv", "it", "id", "hi", "fi", "vi", "he", "uk", "el", "ms", "cs", "ro", "da", "hu", "ta", "no", "th", "ur", "hr", "bg", "lt", "la", "mi", "ml", "cy", "sk", "te", "fa", "lv", "bn", "sr", "az", "sl", "kn", "et", "mk", "br", "eu", "is", "hy", "ne", "mn", "bs", "kk", "sq", "sw", "gl", "mr", "pa", "si", "km", "sn", "yo", "so", "af", "oc", "ka", "be", "tg", "sd", "gu", "am", "yi", "lo", "uz", "fo", "ht", "ps", "tk", "nn", "mt", "sa", "lb", "my", "bo", "tl", "mg", "as", "tt", "haw", "ln", "ha", "ba", "jw", "su"]',
    SupportedFormats = '["mp3", "mp4", "mpeg", "mpga", "m4a", "wav", "webm"]',
    IsDefault = 1,
    DefaultCapabilityType = 'transcription'
WHERE ModelAlias = 'whisper-1'
  AND ProviderCredentialId IN (SELECT Id FROM ProviderCredential WHERE Provider = 'openai');

-- TTS models
UPDATE ModelProviderMapping 
SET SupportsTextToSpeech = 1,
    SupportedVoices = '["alloy", "echo", "fable", "onyx", "nova", "shimmer"]',
    SupportedLanguages = '["en", "es", "fr", "de", "it", "pt", "ru", "zh", "ja", "ko", "nl", "pl", "sv", "da", "no", "fi", "tr", "ar", "he", "hi"]',
    SupportedFormats = '["mp3", "opus", "aac", "flac", "wav", "pcm"]',
    IsDefault = 1,
    DefaultCapabilityType = 'tts'
WHERE ModelAlias = 'tts-1'
  AND ProviderCredentialId IN (SELECT Id FROM ProviderCredential WHERE Provider = 'openai');

UPDATE ModelProviderMapping 
SET SupportsTextToSpeech = 1,
    SupportedVoices = '["alloy", "echo", "fable", "onyx", "nova", "shimmer"]',
    SupportedLanguages = '["en", "es", "fr", "de", "it", "pt", "ru", "zh", "ja", "ko", "nl", "pl", "sv", "da", "no", "fi", "tr", "ar", "he", "hi"]',
    SupportedFormats = '["mp3", "opus", "aac", "flac", "wav", "pcm"]'
WHERE ModelAlias = 'tts-1-hd'
  AND ProviderCredentialId IN (SELECT Id FROM ProviderCredential WHERE Provider = 'openai');

-- Realtime model
UPDATE ModelProviderMapping 
SET SupportsRealtimeAudio = 1,
    SupportedFormats = '["pcm16_8khz", "pcm16_16khz", "pcm16_24khz", "g711_ulaw", "g711_alaw"]',
    IsDefault = 1,
    DefaultCapabilityType = 'realtime',
    TokenizerType = 'o200k_base'
WHERE ModelAlias = 'gpt-4o-realtime-preview'
  AND ProviderCredentialId IN (SELECT Id FROM ProviderCredential WHERE Provider = 'openai');

-- Set default chat model for OpenAI
UPDATE ModelProviderMapping 
SET IsDefault = 1, DefaultCapabilityType = 'chat'
WHERE ModelAlias = 'gpt-4o'
  AND ProviderCredentialId IN (SELECT Id FROM ProviderCredential WHERE Provider = 'openai')
  AND NOT EXISTS (
    SELECT 1 FROM ModelProviderMapping 
    WHERE ProviderCredentialId IN (SELECT Id FROM ProviderCredential WHERE Provider = 'openai')
    AND IsDefault = 1 
    AND DefaultCapabilityType = 'chat'
  );

-- Anthropic Models
-- Vision-capable Claude 3 models
UPDATE ModelProviderMapping 
SET SupportsVision = 1, TokenizerType = 'claude'
WHERE ModelAlias IN ('claude-3-opus', 'claude-3-sonnet', 'claude-3-haiku', 'claude-3-5-sonnet')
  AND ProviderCredentialId IN (SELECT Id FROM ProviderCredential WHERE Provider = 'anthropic');

-- Set default chat model for Anthropic
UPDATE ModelProviderMapping 
SET IsDefault = 1, DefaultCapabilityType = 'chat'
WHERE ModelAlias = 'claude-3-5-sonnet-20241022'
  AND ProviderCredentialId IN (SELECT Id FROM ProviderCredential WHERE Provider = 'anthropic')
  AND NOT EXISTS (
    SELECT 1 FROM ModelProviderMapping 
    WHERE ProviderCredentialId IN (SELECT Id FROM ProviderCredential WHERE Provider = 'anthropic')
    AND IsDefault = 1 
    AND DefaultCapabilityType = 'chat'
  );

-- Google/Gemini Models
-- Vision-capable models
UPDATE ModelProviderMapping 
SET SupportsVision = 1, TokenizerType = 'gemini'
WHERE ModelAlias IN ('gemini-pro-vision', 'gemini-1.5-pro', 'gemini-1.5-flash')
  AND ProviderCredentialId IN (SELECT Id FROM ProviderCredential WHERE Provider = 'gemini');

-- Set default chat model for Gemini
UPDATE ModelProviderMapping 
SET IsDefault = 1, DefaultCapabilityType = 'chat'
WHERE ModelAlias = 'gemini-1.5-pro'
  AND ProviderCredentialId IN (SELECT Id FROM ProviderCredential WHERE Provider = 'gemini')
  AND NOT EXISTS (
    SELECT 1 FROM ModelProviderMapping 
    WHERE ProviderCredentialId IN (SELECT Id FROM ProviderCredential WHERE Provider = 'gemini')
    AND IsDefault = 1 
    AND DefaultCapabilityType = 'chat'
  );

-- ElevenLabs Models
UPDATE ModelProviderMapping 
SET SupportsTextToSpeech = 1,
    SupportedVoices = '["rachel", "drew", "clyde", "paul", "domi", "dave", "fin", "bella", "antoni", "thomas", "charlie", "emily", "elli", "callum", "patrick", "harry", "liam", "dorothy", "josh", "arnold", "charlotte", "matilda", "matthew", "james", "joseph", "jeremy", "michael", "ethan", "gigi", "freya", "grace", "daniel", "serena", "adam", "nicole", "jessie", "ryan", "sam", "glinda", "giovanni", "mimi"]',
    SupportedLanguages = '["en", "es", "fr", "de", "it", "pt", "pl", "ru", "nl", "sv", "cs", "ar", "zh", "ja", "ko", "hi", "tr", "da", "fi", "el", "he", "hu", "id", "ms", "no", "ro", "sk", "th", "uk", "vi"]',
    SupportedFormats = '["mp3_44100", "pcm_16000", "pcm_22050", "pcm_24000", "pcm_44100"]',
    IsDefault = 1,
    DefaultCapabilityType = 'tts'
WHERE ModelAlias IN ('eleven_multilingual_v2', 'eleven_turbo_v2')
  AND ProviderCredentialId IN (SELECT Id FROM ProviderCredential WHERE Provider = 'elevenlabs');

-- ElevenLabs Realtime
UPDATE ModelProviderMapping 
SET SupportsRealtimeAudio = 1,
    SupportedFormats = '["pcm16_16khz"]'
WHERE ModelAlias = 'conversational-v1'
  AND ProviderCredentialId IN (SELECT Id FROM ProviderCredential WHERE Provider = 'elevenlabs');

-- Ultravox Models
UPDATE ModelProviderMapping 
SET SupportsRealtimeAudio = 1,
    SupportedFormats = '["pcm16_8khz", "pcm16_16khz", "pcm16_24khz"]',
    IsDefault = 1,
    DefaultCapabilityType = 'realtime'
WHERE ModelAlias IN ('ultravox-v0_2', 'ultravox-v2')
  AND ProviderCredentialId IN (SELECT Id FROM ProviderCredential WHERE Provider = 'ultravox');

-- Azure OpenAI Models (similar to OpenAI)
UPDATE ModelProviderMapping 
SET SupportsVision = 1, TokenizerType = 'cl100k_base'
WHERE ModelAlias IN ('gpt-4-vision-preview', 'gpt-4-turbo', 'gpt-4-turbo-preview')
  AND ProviderCredentialId IN (SELECT Id FROM ProviderCredential WHERE Provider = 'azure');

UPDATE ModelProviderMapping 
SET SupportsVision = 1, TokenizerType = 'o200k_base'
WHERE ModelAlias IN ('gpt-4o', 'gpt-4o-mini')
  AND ProviderCredentialId IN (SELECT Id FROM ProviderCredential WHERE Provider = 'azure');

UPDATE ModelProviderMapping 
SET SupportsAudioTranscription = 1,
    SupportedLanguages = '["en", "zh", "de", "es", "ru", "ko", "fr", "ja", "pt", "tr", "pl", "ca", "nl", "ar", "sv", "it", "id", "hi", "fi", "vi", "he", "uk", "el", "ms", "cs", "ro", "da", "hu", "ta", "no", "th", "ur", "hr", "bg", "lt", "la", "mi", "ml", "cy", "sk", "te", "fa", "lv", "bn", "sr", "az", "sl", "kn", "et", "mk", "br", "eu", "is", "hy", "ne", "mn", "bs", "kk", "sq", "sw", "gl", "mr", "pa", "si", "km", "sn", "yo", "so", "af", "oc", "ka", "be", "tg", "sd", "gu", "am", "yi", "lo", "uz", "fo", "ht", "ps", "tk", "nn", "mt", "sa", "lb", "my", "bo", "tl", "mg", "as", "tt", "haw", "ln", "ha", "ba", "jw", "su"]',
    SupportedFormats = '["mp3", "mp4", "mpeg", "mpga", "m4a", "wav", "webm"]'
WHERE ModelAlias = 'whisper-1'
  AND ProviderCredentialId IN (SELECT Id FROM ProviderCredential WHERE Provider = 'azure');

-- Bedrock Models (Claude via AWS)
UPDATE ModelProviderMapping 
SET SupportsVision = 1, TokenizerType = 'claude'
WHERE ModelAlias IN ('claude-3-opus', 'claude-3-sonnet', 'claude-3-haiku')
  AND ProviderCredentialId IN (SELECT Id FROM ProviderCredential WHERE Provider = 'bedrock');

-- VertexAI Models (Gemini via GCP)
UPDATE ModelProviderMapping 
SET SupportsVision = 1, TokenizerType = 'gemini'
WHERE ModelAlias LIKE 'gemini%'
  AND ProviderCredentialId IN (SELECT Id FROM ProviderCredential WHERE Provider = 'vertexai');

-- Legacy models with different tokenizers
UPDATE ModelProviderMapping 
SET TokenizerType = 'p50k_base'
WHERE ModelAlias IN ('davinci', 'curie', 'babbage', 'ada')
  AND TokenizerType IS NULL;

-- Default tokenizer for any remaining models
UPDATE ModelProviderMapping 
SET TokenizerType = 'cl100k_base'
WHERE TokenizerType IS NULL OR TokenizerType = '';

-- Note: This script uses simplified syntax that works in both SQLite and PostgreSQL.
-- For production use, you may want to create provider-specific versions with better error handling.
-- 
-- To apply this script:
-- SQLite: sqlite3 conduit.db < populate-model-capabilities.sql
-- PostgreSQL: psql -U your_user -d your_database -f populate-model-capabilities.sql