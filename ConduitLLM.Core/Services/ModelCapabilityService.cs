using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Service implementation for retrieving model capabilities.
    /// This is a temporary implementation using hardcoded patterns until the database
    /// schema can be extended to support capability fields.
    /// </summary>
    public class ModelCapabilityService : IModelCapabilityService
    {
        private readonly ILogger<ModelCapabilityService> _logger;

        // Hardcoded patterns for model capabilities
        private static readonly HashSet<string> VisionModels = new(StringComparer.OrdinalIgnoreCase)
        {
            "gpt-4-vision-preview", "gpt-4-turbo", "gpt-4-turbo-preview", "gpt-4o", "gpt-4o-mini",
            "claude-3-opus", "claude-3-sonnet", "claude-3-haiku", "claude-3-5-sonnet",
            "gemini-pro-vision", "gemini-1.5-pro", "gemini-1.5-flash"
        };

        private static readonly HashSet<string> AudioTranscriptionModels = new(StringComparer.OrdinalIgnoreCase)
        {
            "whisper-1", "whisper-large-v3"
        };

        private static readonly HashSet<string> TextToSpeechModels = new(StringComparer.OrdinalIgnoreCase)
        {
            "tts-1", "tts-1-hd", "eleven_multilingual_v2", "eleven_turbo_v2"
        };

        private static readonly HashSet<string> RealtimeAudioModels = new(StringComparer.OrdinalIgnoreCase)
        {
            "gpt-4o-realtime-preview", "ultravox-v0_2"
        };

        private static readonly Dictionary<string, string> TokenizerTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-4"] = "cl100k_base",
            ["gpt-4-turbo"] = "cl100k_base",
            ["gpt-4o"] = "o200k_base",
            ["gpt-3.5-turbo"] = "cl100k_base",
            ["claude-3"] = "claude",
            ["gemini"] = "gemini"
        };

        public ModelCapabilityService(ILogger<ModelCapabilityService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public Task<bool> SupportsVisionAsync(string model)
        {
            return Task.FromResult(VisionModels.Contains(model) || 
                                 VisionModels.Any(vm => model.Contains(vm, StringComparison.OrdinalIgnoreCase)));
        }

        /// <inheritdoc/>
        public Task<bool> SupportsAudioTranscriptionAsync(string model)
        {
            return Task.FromResult(AudioTranscriptionModels.Contains(model));
        }

        /// <inheritdoc/>
        public Task<bool> SupportsTextToSpeechAsync(string model)
        {
            return Task.FromResult(TextToSpeechModels.Contains(model));
        }

        /// <inheritdoc/>
        public Task<bool> SupportsRealtimeAudioAsync(string model)
        {
            return Task.FromResult(RealtimeAudioModels.Contains(model));
        }

        /// <inheritdoc/>
        public Task<string?> GetTokenizerTypeAsync(string model)
        {
            // Check exact match first
            if (TokenizerTypes.TryGetValue(model, out var tokenizer))
                return Task.FromResult<string?>(tokenizer);

            // Check if model starts with known prefix
            foreach (var kvp in TokenizerTypes)
            {
                if (model.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult<string?>(kvp.Value);
            }

            // Default tokenizer
            return Task.FromResult<string?>("cl100k_base");
        }

        /// <inheritdoc/>
        public Task<List<string>> GetSupportedVoicesAsync(string model)
        {
            var voices = model switch
            {
                "tts-1" or "tts-1-hd" => new List<string> { "alloy", "echo", "fable", "nova", "onyx", "shimmer" },
                "eleven_multilingual_v2" or "eleven_turbo_v2" => new List<string> { "rachel", "drew", "clyde", "paul", "domi", "dave", "fin", "bella", "antoni", "thomas", "charlie", "emily", "elli", "callum", "patrick", "harry", "liam", "dorothy", "josh", "arnold", "charlotte", "matilda", "matthew", "james", "joseph", "jeremy", "michael", "ethan", "gigi", "freya", "grace", "daniel", "serena", "adam", "nicole", "jessie", "ryan", "sam", "glinda", "giovanni", "mimi" },
                _ => new List<string>()
            };
            return Task.FromResult(voices);
        }

        /// <inheritdoc/>
        public Task<List<string>> GetSupportedLanguagesAsync(string model)
        {
            var languages = model switch
            {
                "whisper-1" or "whisper-large-v3" => new List<string> { "en", "zh", "de", "es", "ru", "ko", "fr", "ja", "pt", "tr", "pl", "ca", "nl", "ar", "sv", "it", "id", "hi", "fi", "vi", "he", "uk", "el", "ms", "cs", "ro", "da", "hu", "ta", "no", "th", "ur", "hr", "bg", "lt", "la", "mi", "ml", "cy", "sk", "te", "fa", "lv", "bn", "sr", "az", "sl", "kn", "et", "mk", "br", "eu", "is", "hy", "ne", "mn", "bs", "kk", "sq", "sw", "gl", "mr", "pa", "si", "km", "sn", "yo", "so", "af", "oc", "ka", "be", "tg", "sd", "gu", "am", "yi", "lo", "uz", "fo", "ht", "ps", "tk", "nn", "mt", "sa", "lb", "my", "bo", "tl", "mg", "as", "tt", "haw", "ln", "ha", "ba", "jw", "su" },
                _ => new List<string>()
            };
            return Task.FromResult(languages);
        }

        /// <inheritdoc/>
        public Task<List<string>> GetSupportedFormatsAsync(string model)
        {
            var formats = model switch
            {
                "tts-1" or "tts-1-hd" => new List<string> { "mp3", "opus", "aac", "flac" },
                "eleven_multilingual_v2" or "eleven_turbo_v2" => new List<string> { "mp3_44100", "pcm_16000", "pcm_22050", "pcm_24000", "pcm_44100" },
                "whisper-1" or "whisper-large-v3" => new List<string> { "json", "text", "srt", "verbose_json", "vtt" },
                _ => new List<string>()
            };
            return Task.FromResult(formats);
        }

        /// <inheritdoc/>
        public Task<string?> GetDefaultModelAsync(string provider, string capabilityType)
        {
            var defaultModel = (provider.ToLowerInvariant(), capabilityType.ToLowerInvariant()) switch
            {
                ("openai", "chat") => "gpt-4o",
                ("openai", "transcription") => "whisper-1",
                ("openai", "tts") => "tts-1",
                ("openai", "realtime") => "gpt-4o-realtime-preview",
                ("anthropic", "chat") => "claude-3-5-sonnet-20241022",
                ("gemini", "chat") => "gemini-1.5-pro",
                ("elevenlabs", "tts") => "eleven_multilingual_v2",
                ("ultravox", "realtime") => "ultravox-v0_2",
                _ => null
            };
            return Task.FromResult(defaultModel);
        }

        /// <inheritdoc/>
        public Task RefreshCacheAsync()
        {
            // No cache to refresh in this implementation
            _logger.LogInformation("Model capability cache refresh requested (no-op in hardcoded implementation)");
            return Task.CompletedTask;
        }
    }
}