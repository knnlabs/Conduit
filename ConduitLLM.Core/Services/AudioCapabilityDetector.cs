using System;
using System.Collections.Generic;
using System.Linq;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Default implementation of the audio capability detector.
    /// </summary>
    public class AudioCapabilityDetector : IAudioCapabilityDetector
    {
        // Provider capability definitions
        private readonly Dictionary<string, AudioProviderCapabilities> _providerCapabilities;

        /// <summary>
        /// Initializes a new instance of the AudioCapabilityDetector class.
        /// </summary>
        public AudioCapabilityDetector()
        {
            _providerCapabilities = InitializeProviderCapabilities();
        }

        /// <summary>
        /// Determines if a provider supports audio transcription.
        /// </summary>
        public bool SupportsTranscription(string provider, string? model = null)
        {
            if (!_providerCapabilities.TryGetValue(provider.ToLowerInvariant(), out var capabilities))
                return false;

            if (capabilities.Transcription == null)
                return false;

            // If a specific model is requested, check if it's supported
            if (!string.IsNullOrWhiteSpace(model))
            {
                return capabilities.Transcription.Models.Any(m => 
                    m.ModelId.Equals(model, StringComparison.OrdinalIgnoreCase));
            }

            return true;
        }

        /// <summary>
        /// Determines if a provider supports text-to-speech synthesis.
        /// </summary>
        public bool SupportsTextToSpeech(string provider, string? model = null)
        {
            if (!_providerCapabilities.TryGetValue(provider.ToLowerInvariant(), out var capabilities))
                return false;

            if (capabilities.TextToSpeech == null)
                return false;

            // If a specific model is requested, check if it's supported
            if (!string.IsNullOrWhiteSpace(model))
            {
                return capabilities.TextToSpeech.Models.Any(m => 
                    m.ModelId.Equals(model, StringComparison.OrdinalIgnoreCase));
            }

            return true;
        }

        /// <summary>
        /// Determines if a provider supports real-time conversational audio.
        /// </summary>
        public bool SupportsRealtime(string provider, string? model = null)
        {
            if (!_providerCapabilities.TryGetValue(provider.ToLowerInvariant(), out var capabilities))
                return false;

            return capabilities.Realtime != null;
        }

        /// <summary>
        /// Checks if a specific voice is available for a provider.
        /// </summary>
        public bool SupportsVoice(string provider, string voiceId)
        {
            if (!_providerCapabilities.TryGetValue(provider.ToLowerInvariant(), out var capabilities))
                return false;

            // Check TTS voices
            if (capabilities.TextToSpeech?.Voices != null)
            {
                if (capabilities.TextToSpeech.Voices.Any(v => 
                    v.VoiceId.Equals(voiceId, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }

            // Check real-time voices
            if (capabilities.Realtime?.AvailableVoices != null)
            {
                if (capabilities.Realtime.AvailableVoices.Any(v => 
                    v.VoiceId.Equals(voiceId, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the audio formats supported by a provider for a specific operation.
        /// </summary>
        public AudioFormat[] GetSupportedFormats(string provider, AudioOperation operation)
        {
            if (!_providerCapabilities.TryGetValue(provider.ToLowerInvariant(), out var capabilities))
                return Array.Empty<AudioFormat>();

            return operation switch
            {
                AudioOperation.Transcription => ParseFormatsToEnum(capabilities.Transcription?.SupportedFormats),
                AudioOperation.TextToSpeech => capabilities.TextToSpeech?.SupportedFormats.ToArray() ?? Array.Empty<AudioFormat>(),
                AudioOperation.Realtime => (capabilities.Realtime?.SupportedInputFormats ?? new List<RealtimeAudioFormat>())
                    .Select(f => ConvertRealtimeToAudioFormat(f))
                    .Where(f => f.HasValue)
                    .Select(f => f!.Value)
                    .ToArray(),
                _ => Array.Empty<AudioFormat>()
            };
        }

        /// <summary>
        /// Gets the languages supported by a provider for a specific audio operation.
        /// </summary>
        public IEnumerable<string> GetSupportedLanguages(string provider, AudioOperation operation)
        {
            if (!_providerCapabilities.TryGetValue(provider.ToLowerInvariant(), out var capabilities))
                return Enumerable.Empty<string>();

            return operation switch
            {
                AudioOperation.Transcription => capabilities.Transcription?.SupportedLanguages ?? Enumerable.Empty<string>(),
                AudioOperation.TextToSpeech => capabilities.TextToSpeech?.SupportedLanguages ?? Enumerable.Empty<string>(),
                AudioOperation.Realtime => capabilities.Realtime?.SupportedLanguages ?? Enumerable.Empty<string>(),
                _ => Enumerable.Empty<string>()
            };
        }

        /// <summary>
        /// Validates that an audio request can be processed by the specified provider.
        /// </summary>
        public bool ValidateAudioRequest(AudioRequestBase request, string provider, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (!request.IsValid(out var validationError))
            {
                errorMessage = validationError ?? "Request validation failed";
                return false;
            }

            if (!_providerCapabilities.TryGetValue(provider.ToLowerInvariant(), out var capabilities))
            {
                errorMessage = $"Unknown provider: {provider}";
                return false;
            }

            // Additional provider-specific validation could be added here

            return true;
        }

        /// <summary>
        /// Gets a list of all providers that support a specific audio capability.
        /// </summary>
        public IEnumerable<string> GetProvidersWithCapability(AudioCapability capability)
        {
            return _providerCapabilities
                .Where(kvp => HasCapability(kvp.Value, capability))
                .Select(kvp => kvp.Key);
        }

        /// <summary>
        /// Gets detailed capability information for a specific provider.
        /// </summary>
        public AudioProviderCapabilities GetProviderCapabilities(string provider)
        {
            return _providerCapabilities.TryGetValue(provider.ToLowerInvariant(), out var capabilities)
                ? capabilities
                : new AudioProviderCapabilities { Provider = provider };
        }

        /// <summary>
        /// Determines the best provider for a specific audio request.
        /// </summary>
        public string? RecommendProvider(AudioRequestBase request, IEnumerable<string> availableProviders)
        {
            var providers = availableProviders.ToList();
            if (!providers.Any())
                return null;

            // This is a simplified recommendation logic
            // In a real implementation, this would consider:
            // - Cost optimization
            // - Quality requirements
            // - Language/voice support
            // - Provider health/availability

            // For now, prefer providers in this order for different request types
            if (request is AudioTranscriptionRequest)
            {
                // Prefer OpenAI for transcription (Whisper is excellent)
                if (providers.Contains("openai", StringComparer.OrdinalIgnoreCase))
                    return "openai";
                if (providers.Contains("azure", StringComparer.OrdinalIgnoreCase))
                    return "azure";
            }
            else if (request is TextToSpeechRequest ttsRequest)
            {
                // For TTS, consider voice requirements
                foreach (var provider in providers)
                {
                    if (SupportsVoice(provider, ttsRequest.Voice))
                        return provider;
                }
            }

            // Default to first available
            return providers.First();
        }

        /// <summary>
        /// Initializes the provider capability definitions.
        /// </summary>
        private Dictionary<string, AudioProviderCapabilities> InitializeProviderCapabilities()
        {
            return new Dictionary<string, AudioProviderCapabilities>(StringComparer.OrdinalIgnoreCase)
            {
                ["openai"] = CreateOpenAICapabilities(),
                ["azure"] = CreateAzureOpenAICapabilities(),
                ["google"] = CreateGoogleCapabilities(),
                ["elevenlabs"] = CreateElevenLabsCapabilities(),
                ["ultravox"] = CreateUltravoxCapabilities()
            };
        }

        private AudioProviderCapabilities CreateOpenAICapabilities()
        {
            return new AudioProviderCapabilities
            {
                Provider = "openai",
                DisplayName = "OpenAI",
                SupportedCapabilities = new List<AudioCapability>
                {
                    AudioCapability.BasicTranscription,
                    AudioCapability.TimestampedTranscription,
                    AudioCapability.BasicTTS,
                    AudioCapability.MultiVoiceTTS,
                    AudioCapability.StreamingAudio,
                    AudioCapability.RealtimeConversation,
                    AudioCapability.RealtimeFunctions
                },
                Transcription = new TranscriptionCapabilities
                {
                    SupportedFormats = new List<string> { "mp3", "mp4", "mpeg", "mpga", "m4a", "wav", "webm" },
                    SupportedLanguages = GetWhisperLanguages(),
                    Models = new List<AudioModelInfo>
                    {
                        new AudioModelInfo { ModelId = "whisper-1", Name = "Whisper v1", IsDefault = true }
                    },
                    SupportsAutoLanguageDetection = true,
                    SupportsWordTimestamps = true,
                    MaxFileSizeBytes = 25 * 1024 * 1024, // 25MB
                    OutputFormats = new List<TranscriptionFormat>
                    {
                        TranscriptionFormat.Json,
                        TranscriptionFormat.Text,
                        TranscriptionFormat.Srt,
                        TranscriptionFormat.Vtt,
                        TranscriptionFormat.VerboseJson
                    }
                },
                TextToSpeech = new TextToSpeechCapabilities
                {
                    Voices = GetOpenAIVoices(),
                    SupportedFormats = new List<AudioFormat>
                    {
                        AudioFormat.Mp3,
                        AudioFormat.Opus,
                        AudioFormat.Aac,
                        AudioFormat.Flac,
                        AudioFormat.Wav,
                        AudioFormat.Pcm
                    },
                    Models = new List<AudioModelInfo>
                    {
                        new AudioModelInfo { ModelId = "tts-1", Name = "TTS v1", IsDefault = true },
                        new AudioModelInfo { ModelId = "tts-1-hd", Name = "TTS v1 HD" }
                    },
                    SupportedLanguages = GetOpenAISupportedTTSLanguages(),
                    SupportsStreaming = false, // Not yet, but we simulate it
                    SpeedRange = new RangeLimit { Min = 0.25, Max = 4.0, Default = 1.0 },
                    MaxTextLength = 4096
                },
                Realtime = new RealtimeCapabilities
                {
                    SupportedInputFormats = new List<RealtimeAudioFormat>
                    {
                        RealtimeAudioFormat.PCM16_8kHz,
                        RealtimeAudioFormat.PCM16_16kHz,
                        RealtimeAudioFormat.PCM16_24kHz,
                        RealtimeAudioFormat.G711_ULAW,
                        RealtimeAudioFormat.G711_ALAW
                    },
                    SupportedOutputFormats = new List<RealtimeAudioFormat>
                    {
                        RealtimeAudioFormat.PCM16_8kHz,
                        RealtimeAudioFormat.PCM16_16kHz,
                        RealtimeAudioFormat.PCM16_24kHz
                    },
                    AvailableVoices = GetOpenAIVoices(),
                    SupportedLanguages = GetOpenAISupportedTTSLanguages(),
                    SupportsFunctionCalling = true,
                    SupportsInterruptions = true,
                    MaxSessionDurationSeconds = 900 // 15 minutes
                }
            };
        }

        private AudioProviderCapabilities CreateAzureOpenAICapabilities()
        {
            // Azure OpenAI has similar capabilities to OpenAI
            var capabilities = CreateOpenAICapabilities();
            capabilities.Provider = "azure";
            capabilities.DisplayName = "Azure OpenAI";
            return capabilities;
        }

        private AudioProviderCapabilities CreateGoogleCapabilities()
        {
            return new AudioProviderCapabilities
            {
                Provider = "google",
                DisplayName = "Google Cloud",
                SupportedCapabilities = new List<AudioCapability>
                {
                    AudioCapability.BasicTranscription,
                    AudioCapability.TimestampedTranscription,
                    AudioCapability.BasicTTS,
                    AudioCapability.MultiVoiceTTS,
                    AudioCapability.SSMLSupport
                },
                Transcription = new TranscriptionCapabilities
                {
                    SupportedFormats = new List<string> { "wav", "flac", "mp3", "ogg", "webm" },
                    SupportedLanguages = GetGoogleSupportedLanguages(),
                    SupportsAutoLanguageDetection = true,
                    SupportsWordTimestamps = true,
                    SupportsSpeakerDiarization = true,
                    MaxFileSizeBytes = 180 * 1024 * 1024 // 180MB
                },
                TextToSpeech = new TextToSpeechCapabilities
                {
                    SupportedFormats = new List<AudioFormat>
                    {
                        AudioFormat.Mp3,
                        AudioFormat.Wav,
                        AudioFormat.Ogg
                    },
                    SupportedLanguages = GetGoogleSupportedLanguages(),
                    SupportsSSML = true,
                    MaxTextLength = 5000
                }
            };
        }

        private AudioProviderCapabilities CreateElevenLabsCapabilities()
        {
            return new AudioProviderCapabilities
            {
                Provider = "elevenlabs",
                DisplayName = "ElevenLabs",
                SupportedCapabilities = new List<AudioCapability>
                {
                    AudioCapability.BasicTTS,
                    AudioCapability.MultiVoiceTTS,
                    AudioCapability.EmotionalTTS,
                    AudioCapability.VoiceCloning,
                    AudioCapability.StreamingAudio,
                    AudioCapability.RealtimeConversation
                },
                TextToSpeech = new TextToSpeechCapabilities
                {
                    SupportedFormats = new List<AudioFormat>
                    {
                        AudioFormat.Mp3,
                        AudioFormat.Wav,
                        AudioFormat.Flac,
                        AudioFormat.Ogg,
                        AudioFormat.Pcm
                    },
                    SupportedLanguages = GetElevenLabsSupportedLanguages(),
                    SupportsStreaming = true,
                    SupportsVoiceCloning = true,
                    MaxTextLength = 5000
                },
                Realtime = new RealtimeCapabilities
                {
                    SupportedInputFormats = new List<RealtimeAudioFormat>
                    {
                        RealtimeAudioFormat.PCM16_16kHz
                    },
                    SupportedOutputFormats = new List<RealtimeAudioFormat>
                    {
                        RealtimeAudioFormat.PCM16_16kHz
                    },
                    SupportsInterruptions = true,
                    MaxSessionDurationSeconds = 1800 // 30 minutes
                }
            };
        }

        private AudioProviderCapabilities CreateUltravoxCapabilities()
        {
            return new AudioProviderCapabilities
            {
                Provider = "ultravox",
                DisplayName = "Ultravox",
                SupportedCapabilities = new List<AudioCapability>
                {
                    AudioCapability.RealtimeConversation,
                    AudioCapability.RealtimeFunctions
                },
                Realtime = new RealtimeCapabilities
                {
                    SupportedInputFormats = new List<RealtimeAudioFormat>
                    {
                        RealtimeAudioFormat.PCM16_8kHz,
                        RealtimeAudioFormat.PCM16_16kHz,
                        RealtimeAudioFormat.PCM16_24kHz
                    },
                    SupportedOutputFormats = new List<RealtimeAudioFormat>
                    {
                        RealtimeAudioFormat.PCM16_16kHz
                    },
                    SupportsFunctionCalling = true,
                    SupportsInterruptions = true
                }
            };
        }

        private bool HasCapability(AudioProviderCapabilities capabilities, AudioCapability capability)
        {
            return capabilities.SupportedCapabilities?.Contains(capability) ?? false;
        }

        private AudioFormat[] ParseFormatsToEnum(List<string>? formats)
        {
            if (formats == null) return Array.Empty<AudioFormat>();

            var result = new List<AudioFormat>();
            foreach (var format in formats)
            {
                if (Enum.TryParse<AudioFormat>(format, true, out var audioFormat))
                {
                    result.Add(audioFormat);
                }
            }
            return result.ToArray();
        }

        private AudioFormat? ConvertRealtimeToAudioFormat(RealtimeAudioFormat format)
        {
            return format switch
            {
                RealtimeAudioFormat.MP3 => AudioFormat.Mp3,
                RealtimeAudioFormat.Opus => AudioFormat.Opus,
                _ => null
            };
        }

        private List<string> GetWhisperLanguages()
        {
            return new List<string>
            {
                "en", "zh", "de", "es", "ru", "ko", "fr", "ja", "pt", "tr",
                "pl", "ca", "nl", "ar", "sv", "it", "id", "hi", "fi", "vi",
                "he", "uk", "el", "ms", "cs", "ro", "da", "hu", "ta", "no",
                "th", "ur", "hr", "bg", "lt", "la", "mi", "ml", "cy", "sk",
                "te", "fa", "lv", "bn", "sr", "az", "sl", "kn", "et", "mk",
                "br", "eu", "is", "hy", "ne", "mn", "bs", "kk", "sq", "sw",
                "gl", "mr", "pa", "si", "km", "sn", "yo", "so", "af", "oc",
                "ka", "be", "tg", "sd", "gu", "am", "yi", "lo", "uz", "fo",
                "ht", "ps", "tk", "nn", "mt", "sa", "lb", "my", "bo", "tl",
                "mg", "as", "tt", "haw", "ln", "ha", "ba", "jw", "su"
            };
        }

        private List<VoiceInfo> GetOpenAIVoices()
        {
            return new List<VoiceInfo>
            {
                new VoiceInfo { VoiceId = "alloy", Name = "Alloy", Gender = VoiceGender.Neutral },
                new VoiceInfo { VoiceId = "echo", Name = "Echo", Gender = VoiceGender.Male },
                new VoiceInfo { VoiceId = "fable", Name = "Fable", Gender = VoiceGender.Male },
                new VoiceInfo { VoiceId = "onyx", Name = "Onyx", Gender = VoiceGender.Male },
                new VoiceInfo { VoiceId = "nova", Name = "Nova", Gender = VoiceGender.Female },
                new VoiceInfo { VoiceId = "shimmer", Name = "Shimmer", Gender = VoiceGender.Female }
            };
        }

        private List<string> GetOpenAISupportedTTSLanguages()
        {
            // OpenAI TTS supports many languages but not as many as Whisper
            return new List<string>
            {
                "en", "es", "fr", "de", "it", "pt", "ru", "zh", "ja", "ko",
                "nl", "pl", "sv", "da", "no", "fi", "tr", "ar", "he", "hi"
            };
        }

        private List<string> GetGoogleSupportedLanguages()
        {
            // Simplified list - Google supports many more
            return new List<string>
            {
                "en", "es", "fr", "de", "it", "pt", "ru", "zh", "ja", "ko",
                "nl", "pl", "sv", "da", "no", "fi", "tr", "ar", "he", "hi",
                "th", "vi", "id", "ms", "fil", "uk", "cs", "ro", "hu", "el"
            };
        }

        private List<string> GetElevenLabsSupportedLanguages()
        {
            // ElevenLabs supports multiple languages
            return new List<string>
            {
                "en", "es", "fr", "de", "it", "pt", "pl", "ru", "nl", "sv",
                "cs", "ar", "zh", "ja", "ko", "hi", "tr", "da", "fi", "el",
                "he", "hu", "id", "ms", "no", "ro", "sk", "th", "uk", "vi"
            };
        }
    }
}