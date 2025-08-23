using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Core.Models.Audio
{
    /// <summary>
    /// Comprehensive capability information for an audio provider.
    /// </summary>
    public class AudioProviderCapabilities
    {
        /// <summary>
        /// The provider name.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Provider display name.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Transcription capabilities.
        /// </summary>
        public TranscriptionCapabilities? Transcription { get; set; }

        /// <summary>
        /// Text-to-speech capabilities.
        /// </summary>
        public TextToSpeechCapabilities? TextToSpeech { get; set; }

        /// <summary>
        /// Real-time conversation capabilities.
        /// </summary>
        public RealtimeCapabilities? Realtime { get; set; }

        /// <summary>
        /// General audio capabilities.
        /// </summary>
        public List<AudioCapability> SupportedCapabilities { get; set; } = new();

        /// <summary>
        /// Provider-specific limitations.
        /// </summary>
        public AudioLimitations? Limitations { get; set; }

        /// <summary>
        /// Cost information for audio operations.
        /// </summary>
        public AudioCostInfo? CostInfo { get; set; }

        /// <summary>
        /// Quality ratings for different operations.
        /// </summary>
        public QualityRatings? Quality { get; set; }

        /// <summary>
        /// Regional availability.
        /// </summary>
        public List<string>? AvailableRegions { get; set; }

        /// <summary>
        /// Provider-specific features.
        /// </summary>
        public Dictionary<string, object>? CustomFeatures { get; set; }
    }

    /// <summary>
    /// Transcription-specific capabilities.
    /// </summary>
    public class TranscriptionCapabilities
    {
        /// <summary>
        /// Supported audio input formats.
        /// </summary>
        public List<string> SupportedFormats { get; set; } = new();

        /// <summary>
        /// Supported languages for transcription.
        /// </summary>
        public List<string> SupportedLanguages { get; set; } = new();

        /// <summary>
        /// Available transcription models.
        /// </summary>
        public List<AudioModelInfo> Models { get; set; } = new();

        /// <summary>
        /// Whether automatic language detection is supported.
        /// </summary>
        public bool SupportsAutoLanguageDetection { get; set; }

        /// <summary>
        /// Whether word-level timestamps are supported.
        /// </summary>
        public bool SupportsWordTimestamps { get; set; }

        /// <summary>
        /// Whether speaker diarization is supported.
        /// </summary>
        public bool SupportsSpeakerDiarization { get; set; }

        /// <summary>
        /// Whether punctuation can be controlled.
        /// </summary>
        public bool SupportsPunctuationControl { get; set; }

        /// <summary>
        /// Whether profanity filtering is available.
        /// </summary>
        public bool SupportsProfanityFilter { get; set; }

        /// <summary>
        /// Maximum audio file size in bytes.
        /// </summary>
        public long? MaxFileSizeBytes { get; set; }

        /// <summary>
        /// Maximum audio duration in seconds.
        /// </summary>
        public int? MaxDurationSeconds { get; set; }

        /// <summary>
        /// Supported output formats.
        /// </summary>
        public List<TranscriptionFormat> OutputFormats { get; set; } = new();
    }

    /// <summary>
    /// Text-to-speech specific capabilities.
    /// </summary>
    public class TextToSpeechCapabilities
    {
        /// <summary>
        /// Available voices.
        /// </summary>
        public List<VoiceInfo> Voices { get; set; } = new();

        /// <summary>
        /// Supported output audio formats.
        /// </summary>
        public List<AudioFormat> SupportedFormats { get; set; } = new();

        /// <summary>
        /// Available TTS models.
        /// </summary>
        public List<AudioModelInfo> Models { get; set; } = new();

        /// <summary>
        /// Supported languages.
        /// </summary>
        public List<string> SupportedLanguages { get; set; } = new();

        /// <summary>
        /// Whether SSML is supported.
        /// </summary>
        public bool SupportsSSML { get; set; }

        /// <summary>
        /// Whether streaming output is supported.
        /// </summary>
        public bool SupportsStreaming { get; set; }

        /// <summary>
        /// Whether voice cloning is available.
        /// </summary>
        public bool SupportsVoiceCloning { get; set; }

        /// <summary>
        /// Speed adjustment range.
        /// </summary>
        public RangeLimit? SpeedRange { get; set; }

        /// <summary>
        /// Pitch adjustment range.
        /// </summary>
        public RangeLimit? PitchRange { get; set; }

        /// <summary>
        /// Maximum input text length.
        /// </summary>
        public int? MaxTextLength { get; set; }

        /// <summary>
        /// Available voice styles.
        /// </summary>
        public List<string>? VoiceStyles { get; set; }
    }

    /// <summary>
    /// Information about an audio model.
    /// </summary>
    public class AudioModelInfo
    {
        /// <summary>
        /// Model identifier.
        /// </summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// Model display name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Model description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Model version.
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// Whether this is the default model.
        /// </summary>
        public bool IsDefault { get; set; }

        /// <summary>
        /// Model-specific capabilities.
        /// </summary>
        public Dictionary<string, object>? Capabilities { get; set; }
    }

    /// <summary>
    /// Audio operation limitations.
    /// </summary>
    public class AudioLimitations
    {
        /// <summary>
        /// Rate limits per minute.
        /// </summary>
        public Dictionary<string, int>? RateLimitsPerMinute { get; set; }

        /// <summary>
        /// Concurrent request limits.
        /// </summary>
        public Dictionary<string, int>? ConcurrentLimits { get; set; }

        /// <summary>
        /// Daily quota limits.
        /// </summary>
        public Dictionary<string, long>? DailyQuotas { get; set; }

        /// <summary>
        /// File size limitations.
        /// </summary>
        public Dictionary<string, long>? FileSizeLimits { get; set; }

        /// <summary>
        /// Duration limitations in seconds.
        /// </summary>
        public Dictionary<string, int>? DurationLimits { get; set; }

        /// <summary>
        /// API-specific restrictions.
        /// </summary>
        public List<string>? Restrictions { get; set; }
    }

    /// <summary>
    /// Cost information for audio operations.
    /// </summary>
    public class AudioCostInfo
    {
        /// <summary>
        /// Cost per minute of transcription.
        /// </summary>
        public decimal? TranscriptionPerMinute { get; set; }

        /// <summary>
        /// Cost per 1K characters for TTS.
        /// </summary>
        public decimal? TextToSpeechPer1KChars { get; set; }

        /// <summary>
        /// Cost per minute for real-time conversation.
        /// </summary>
        public decimal? RealtimePerMinute { get; set; }

        /// <summary>
        /// Currency for the costs (e.g., "USD").
        /// </summary>
        public string Currency { get; set; } = "USD";

        /// <summary>
        /// Model-specific pricing.
        /// </summary>
        public Dictionary<string, decimal>? ModelPricing { get; set; }

        /// <summary>
        /// Additional cost factors.
        /// </summary>
        public Dictionary<string, object>? AdditionalCosts { get; set; }
    }

    /// <summary>
    /// Quality ratings for audio operations.
    /// </summary>
    public class QualityRatings
    {
        /// <summary>
        /// Transcription accuracy rating (0-100).
        /// </summary>
        public int? TranscriptionAccuracy { get; set; }

        /// <summary>
        /// TTS naturalness rating (0-100).
        /// </summary>
        public int? TTSNaturalness { get; set; }

        /// <summary>
        /// Real-time latency rating (0-100, lower is better).
        /// </summary>
        public int? RealtimeLatency { get; set; }

        /// <summary>
        /// Overall reliability rating (0-100).
        /// </summary>
        public int? Reliability { get; set; }

        /// <summary>
        /// Language coverage rating (0-100).
        /// </summary>
        public int? LanguageCoverage { get; set; }
    }

    /// <summary>
    /// Represents a numeric range limit.
    /// </summary>
    public class RangeLimit
    {
        /// <summary>
        /// Minimum value.
        /// </summary>
        public double Min { get; set; }

        /// <summary>
        /// Maximum value.
        /// </summary>
        public double Max { get; set; }

        /// <summary>
        /// Default value.
        /// </summary>
        public double Default { get; set; }

        /// <summary>
        /// Step increment.
        /// </summary>
        public double? Step { get; set; }
    }
}
