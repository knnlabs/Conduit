-- Seed Model Capabilities for Existing Models
-- This script populates the new capability columns in ModelProviderMapping table

-- OpenAI Vision Models
UPDATE ModelProviderMapping 
SET 
    SupportsVision = 1,
    TokenizerType = 'cl100k_base'
WHERE 
    ProviderModelName IN ('gpt-4-vision-preview', 'gpt-4-turbo', 'gpt-4v', 'gpt-4o')
    AND EXISTS (SELECT 1 FROM ProviderCredential pc WHERE pc.Id = ModelProviderMapping.ProviderCredentialId AND pc.ProviderName = 'openai');

-- OpenAI Audio Models
UPDATE ModelProviderMapping 
SET 
    SupportsAudioTranscription = 1,
    SupportedLanguages = '["en", "es", "fr", "de", "it", "pt", "ru", "ja", "ko", "zh"]',
    SupportedFormats = '["mp3", "mp4", "mpeg", "mpga", "m4a", "wav", "webm"]',
    IsDefault = 1,
    DefaultCapabilityType = 'transcription'
WHERE 
    ProviderModelName = 'whisper-1'
    AND EXISTS (SELECT 1 FROM ProviderCredential pc WHERE pc.Id = ModelProviderMapping.ProviderCredentialId AND pc.ProviderName = 'openai');

UPDATE ModelProviderMapping 
SET 
    SupportsTextToSpeech = 1,
    SupportedVoices = '["alloy", "echo", "fable", "onyx", "nova", "shimmer"]',
    SupportedLanguages = '["en", "es", "fr", "de", "it", "pt", "ru", "ja", "ko", "zh"]',
    SupportedFormats = '["mp3", "opus", "aac", "flac", "wav", "pcm"]',
    IsDefault = 1,
    DefaultCapabilityType = 'tts'
WHERE 
    ProviderModelName = 'tts-1'
    AND EXISTS (SELECT 1 FROM ProviderCredential pc WHERE pc.Id = ModelProviderMapping.ProviderCredentialId AND pc.ProviderName = 'openai');

UPDATE ModelProviderMapping 
SET 
    SupportsTextToSpeech = 1,
    SupportedVoices = '["alloy", "echo", "fable", "onyx", "nova", "shimmer"]',
    SupportedLanguages = '["en", "es", "fr", "de", "it", "pt", "ru", "ja", "ko", "zh"]',
    SupportedFormats = '["mp3", "opus", "aac", "flac", "wav", "pcm"]'
WHERE 
    ProviderModelName = 'tts-1-hd'
    AND EXISTS (SELECT 1 FROM ProviderCredential pc WHERE pc.Id = ModelProviderMapping.ProviderCredentialId AND pc.ProviderName = 'openai');

-- OpenAI Realtime Models
UPDATE ModelProviderMapping 
SET 
    SupportsRealtimeAudio = 1,
    IsDefault = 1,
    DefaultCapabilityType = 'realtime'
WHERE 
    ProviderModelName = 'gpt-4o-realtime-preview'
    AND EXISTS (SELECT 1 FROM ProviderCredential pc WHERE pc.Id = ModelProviderMapping.ProviderCredentialId AND pc.ProviderName = 'openai');

-- Anthropic Vision Models
UPDATE ModelProviderMapping 
SET 
    SupportsVision = 1,
    TokenizerType = 'claude'
WHERE 
    ProviderModelName IN ('claude-3-opus', 'claude-3-sonnet', 'claude-3-haiku', 'claude-3-opus-20240229', 'claude-3-sonnet-20240229', 'claude-3-haiku-20240307')
    AND EXISTS (SELECT 1 FROM ProviderCredential pc WHERE pc.Id = ModelProviderMapping.ProviderCredentialId AND pc.ProviderName = 'anthropic');

-- Gemini Vision Models
UPDATE ModelProviderMapping 
SET 
    SupportsVision = 1
WHERE 
    ProviderModelName IN ('gemini-pro', 'gemini-pro-vision', 'gemini-1.5-pro', 'gemini-1.5-flash')
    AND EXISTS (SELECT 1 FROM ProviderCredential pc WHERE pc.Id = ModelProviderMapping.ProviderCredentialId AND pc.ProviderName IN ('gemini', 'vertexai'));

-- ElevenLabs Audio Models
UPDATE ModelProviderMapping 
SET 
    SupportsTextToSpeech = 1,
    SupportedLanguages = '["en", "es", "fr", "de", "it", "pt", "ru", "ja", "ko", "zh", "ar", "cs", "da", "nl", "fi", "el", "he", "hi", "hu", "id", "no", "pl", "sv", "ta", "th", "tr", "uk"]',
    SupportedFormats = '["mp3", "opus", "aac", "flac", "wav", "pcm"]'
WHERE 
    EXISTS (SELECT 1 FROM ProviderCredential pc WHERE pc.Id = ModelProviderMapping.ProviderCredentialId AND pc.ProviderName = 'elevenlabs');

-- ElevenLabs Realtime
UPDATE ModelProviderMapping 
SET 
    SupportsRealtimeAudio = 1,
    IsDefault = 1,
    DefaultCapabilityType = 'realtime'
WHERE 
    ProviderModelName = 'conversational-v1'
    AND EXISTS (SELECT 1 FROM ProviderCredential pc WHERE pc.Id = ModelProviderMapping.ProviderCredentialId AND pc.ProviderName = 'elevenlabs');

-- Ultravox Realtime
UPDATE ModelProviderMapping 
SET 
    SupportsRealtimeAudio = 1,
    IsDefault = 1,
    DefaultCapabilityType = 'realtime'
WHERE 
    ProviderModelName = 'ultravox-v2'
    AND EXISTS (SELECT 1 FROM ProviderCredential pc WHERE pc.Id = ModelProviderMapping.ProviderCredentialId AND pc.ProviderName = 'ultravox');

-- Set tokenizer types for GPT models
UPDATE ModelProviderMapping 
SET 
    TokenizerType = 'cl100k_base'
WHERE 
    (ProviderModelName LIKE 'gpt-3.5%' OR ProviderModelName LIKE 'gpt-4%')
    AND EXISTS (SELECT 1 FROM ProviderCredential pc WHERE pc.Id = ModelProviderMapping.ProviderCredentialId AND pc.ProviderName = 'openai');

-- Set tokenizer types for legacy OpenAI models
UPDATE ModelProviderMapping 
SET 
    TokenizerType = 'p50k_base'
WHERE 
    ProviderModelName IN ('davinci', 'curie', 'babbage', 'ada', 'text-davinci-003', 'text-davinci-002')
    AND EXISTS (SELECT 1 FROM ProviderCredential pc WHERE pc.Id = ModelProviderMapping.ProviderCredentialId AND pc.ProviderName = 'openai');

-- Set default chat models
UPDATE ModelProviderMapping 
SET 
    IsDefault = 1,
    DefaultCapabilityType = 'chat'
WHERE 
    ProviderModelName = 'gpt-4o'
    AND EXISTS (SELECT 1 FROM ProviderCredential pc WHERE pc.Id = ModelProviderMapping.ProviderCredentialId AND pc.ProviderName = 'openai');

UPDATE ModelProviderMapping 
SET 
    IsDefault = 1,
    DefaultCapabilityType = 'chat'
WHERE 
    ProviderModelName = 'claude-3-sonnet-20240229'
    AND EXISTS (SELECT 1 FROM ProviderCredential pc WHERE pc.Id = ModelProviderMapping.ProviderCredentialId AND pc.ProviderName = 'anthropic');

UPDATE ModelProviderMapping 
SET 
    IsDefault = 1,
    DefaultCapabilityType = 'chat'
WHERE 
    ProviderModelName = 'gemini-1.5-pro'
    AND EXISTS (SELECT 1 FROM ProviderCredential pc WHERE pc.Id = ModelProviderMapping.ProviderCredentialId AND pc.ProviderName = 'gemini');

-- Update timestamp
UPDATE ModelProviderMapping SET UpdatedAt = CURRENT_TIMESTAMP WHERE UpdatedAt IS NOT NULL;