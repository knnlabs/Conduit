-- Seed audio cost data with accurate provider-specific pricing
-- All costs are in USD

-- OpenAI Audio Costs
INSERT INTO "AudioCosts" ("Provider", "OperationType", "Model", "CostUnit", "CostPerUnit", "Description", "IsActive", "EffectiveFrom", "CreatedAt", "UpdatedAt")
VALUES 
-- Whisper Transcription
('openai', 'transcription', 'whisper-1', 'per-minute', 0.006, 'OpenAI Whisper transcription', true, NOW(), NOW(), NOW()),

-- Text-to-Speech
('openai', 'text-to-speech', 'tts-1', 'per-1k-chars', 0.015, 'OpenAI standard TTS', true, NOW(), NOW(), NOW()),
('openai', 'text-to-speech', 'tts-1-hd', 'per-1k-chars', 0.030, 'OpenAI HD TTS', true, NOW(), NOW(), NOW()),

-- Real-time (GPT-4o)
('openai', 'realtime', 'gpt-4o-realtime-preview-2024-10-01', 'per-minute-input', 0.10, 'OpenAI real-time input audio', true, NOW(), NOW(), NOW()),
('openai', 'realtime', 'gpt-4o-realtime-preview-2024-10-01-output', 'per-minute-output', 0.20, 'OpenAI real-time output audio', true, NOW(), NOW(), NOW()),

-- ElevenLabs TTS Costs
('elevenlabs', 'text-to-speech', 'eleven_monolingual_v1', 'per-1k-chars', 0.030, 'ElevenLabs standard voice', true, NOW(), NOW(), NOW()),
('elevenlabs', 'text-to-speech', 'eleven_multilingual_v2', 'per-1k-chars', 0.060, 'ElevenLabs multilingual voice', true, NOW(), NOW(), NOW()),
('elevenlabs', 'text-to-speech', 'eleven_turbo_v2', 'per-1k-chars', 0.018, 'ElevenLabs turbo voice', true, NOW(), NOW(), NOW()),
('elevenlabs', 'text-to-speech', 'eleven_turbo_v2_5', 'per-1k-chars', 0.022, 'ElevenLabs turbo v2.5', true, NOW(), NOW(), NOW()),

-- Ultravox Real-time Costs
('ultravox', 'realtime', 'fixie-ai/ultravox-v0_2', 'per-minute', 0.001, 'Ultravox v0.2 real-time (1 min minimum)', true, NOW(), NOW(), NOW()),
('ultravox', 'realtime', 'fixie-ai/ultravox-70b', 'per-minute', 0.002, 'Ultravox 70B real-time (1 min minimum)', true, NOW(), NOW(), NOW()),

-- Groq Transcription
('groq', 'transcription', 'whisper-large-v3', 'per-minute', 0.0001, 'Groq Whisper transcription', true, NOW(), NOW(), NOW()),
('groq', 'transcription', 'distil-whisper-large-v3-en', 'per-minute', 0.00005, 'Groq Distil-Whisper English', true, NOW(), NOW(), NOW()),

-- Deepgram Transcription
('deepgram', 'transcription', 'nova-2', 'per-minute', 0.0043, 'Deepgram Nova 2 general', true, NOW(), NOW(), NOW()),
('deepgram', 'transcription', 'nova-2-medical', 'per-minute', 0.0145, 'Deepgram Nova 2 medical', true, NOW(), NOW(), NOW()),
('deepgram', 'transcription', 'nova-2-meeting', 'per-minute', 0.0125, 'Deepgram Nova 2 meeting', true, NOW(), NOW(), NOW()),
('deepgram', 'transcription', 'enhanced', 'per-minute', 0.0145, 'Deepgram Enhanced model', true, NOW(), NOW(), NOW()),
('deepgram', 'transcription', 'base', 'per-minute', 0.0125, 'Deepgram Base model', true, NOW(), NOW(), NOW()),

-- Google Cloud Speech-to-Text
('google', 'transcription', 'latest_long', 'per-minute', 0.006, 'Google Cloud STT standard', true, NOW(), NOW(), NOW()),
('google', 'transcription', 'medical_dictation', 'per-minute', 0.029, 'Google Cloud medical dictation', true, NOW(), NOW(), NOW()),
('google', 'transcription', 'medical_conversation', 'per-minute', 0.049, 'Google Cloud medical conversation', true, NOW(), NOW(), NOW()),
('google', 'transcription', 'chirp', 'per-minute', 0.016, 'Google Cloud Chirp model', true, NOW(), NOW(), NOW()),

-- Google Cloud Text-to-Speech
('google', 'text-to-speech', 'standard', 'per-1k-chars', 0.004, 'Google Cloud TTS standard voices', true, NOW(), NOW(), NOW()),
('google', 'text-to-speech', 'wavenet', 'per-1k-chars', 0.016, 'Google Cloud WaveNet voices', true, NOW(), NOW(), NOW()),
('google', 'text-to-speech', 'neural2', 'per-1k-chars', 0.016, 'Google Cloud Neural2 voices', true, NOW(), NOW(), NOW()),
('google', 'text-to-speech', 'studio', 'per-1k-chars', 0.160, 'Google Cloud Studio voices', true, NOW(), NOW(), NOW()),

-- Amazon Transcribe
('amazon', 'transcription', 'standard', 'per-minute', 0.024, 'Amazon Transcribe standard', true, NOW(), NOW(), NOW()),
('amazon', 'transcription', 'medical', 'per-minute', 0.0775, 'Amazon Transcribe Medical', true, NOW(), NOW(), NOW()),
('amazon', 'transcription', 'call-analytics', 'per-minute', 0.035, 'Amazon Transcribe Call Analytics', true, NOW(), NOW(), NOW()),

-- Amazon Polly TTS
('amazon', 'text-to-speech', 'standard', 'per-1k-chars', 0.004, 'Amazon Polly standard voices', true, NOW(), NOW(), NOW()),
('amazon', 'text-to-speech', 'neural', 'per-1k-chars', 0.016, 'Amazon Polly neural voices', true, NOW(), NOW(), NOW()),
('amazon', 'text-to-speech', 'generative', 'per-1k-chars', 0.030, 'Amazon Polly generative voices', true, NOW(), NOW(), NOW()),

-- Azure Speech Services
('azure', 'transcription', 'standard', 'per-minute', 0.006, 'Azure Speech to Text standard', true, NOW(), NOW(), NOW()),
('azure', 'transcription', 'custom', 'per-minute', 0.018, 'Azure Custom Speech', true, NOW(), NOW(), NOW()),
('azure', 'text-to-speech', 'neural', 'per-1k-chars', 0.016, 'Azure Neural TTS', true, NOW(), NOW(), NOW()),
('azure', 'text-to-speech', 'custom-neural', 'per-1k-chars', 0.023, 'Azure Custom Neural TTS', true, NOW(), NOW(), NOW()),

-- AssemblyAI Transcription
('assemblyai', 'transcription', 'best', 'per-minute', 0.0065, 'AssemblyAI Best model', true, NOW(), NOW(), NOW()),
('assemblyai', 'transcription', 'nano', 'per-minute', 0.0035, 'AssemblyAI Nano model', true, NOW(), NOW(), NOW()),

-- Speechmatics Transcription
('speechmatics', 'transcription', 'enhanced', 'per-minute', 0.0058, 'Speechmatics Enhanced', true, NOW(), NOW(), NOW()),
('speechmatics', 'transcription', 'standard', 'per-minute', 0.0030, 'Speechmatics Standard', true, NOW(), NOW(), NOW())

ON CONFLICT ("Provider", "OperationType", "Model") 
DO UPDATE SET 
    "CostPerUnit" = EXCLUDED."CostPerUnit",
    "Description" = EXCLUDED."Description",
    "UpdatedAt" = NOW();

-- Create indexes for performance
CREATE INDEX IF NOT EXISTS idx_audio_costs_provider ON "AudioCosts" ("Provider");
CREATE INDEX IF NOT EXISTS idx_audio_costs_operation ON "AudioCosts" ("OperationType");
CREATE INDEX IF NOT EXISTS idx_audio_costs_active ON "AudioCosts" ("IsActive");
CREATE INDEX IF NOT EXISTS idx_audio_costs_lookup ON "AudioCosts" ("Provider", "OperationType", "Model", "IsActive");