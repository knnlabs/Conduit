using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Models.Audio;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Defines strategies for routing audio requests to providers.
    /// </summary>
    public interface IAudioRoutingStrategy
    {
        /// <summary>
        /// Gets the name of the routing strategy.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Selects the best provider for a transcription request.
        /// </summary>
        /// <param name="request">The transcription request.</param>
        /// <param name="availableProviders">List of available providers with their capabilities.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The selected provider name, or null if no suitable provider found.</returns>
        Task<string?> SelectTranscriptionProviderAsync(
            AudioTranscriptionRequest request,
            IReadOnlyList<AudioProviderInfo> availableProviders,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Selects the best provider for a text-to-speech request.
        /// </summary>
        /// <param name="request">The TTS request.</param>
        /// <param name="availableProviders">List of available providers with their capabilities.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The selected provider name, or null if no suitable provider found.</returns>
        Task<string?> SelectTextToSpeechProviderAsync(
            TextToSpeechRequest request,
            IReadOnlyList<AudioProviderInfo> availableProviders,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates routing metrics after a request completes.
        /// </summary>
        /// <param name="provider">The provider that handled the request.</param>
        /// <param name="metrics">The performance metrics from the request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task UpdateMetricsAsync(
            string provider,
            AudioRequestMetrics metrics,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Information about an audio provider's capabilities and current status.
    /// </summary>
    public class AudioProviderInfo
    {
        /// <summary>
        /// Gets or sets the provider name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the provider is currently available.
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// Gets or sets the provider's capabilities.
        /// </summary>
        public AudioProviderRoutingCapabilities Capabilities { get; set; } = new();

        /// <summary>
        /// Gets or sets the current performance metrics.
        /// </summary>
        public AudioProviderMetrics Metrics { get; set; } = new();

        /// <summary>
        /// Gets or sets the provider's geographic region.
        /// </summary>
        public string? Region { get; set; }

        /// <summary>
        /// Gets or sets the provider's cost per unit.
        /// </summary>
        public AudioProviderCosts Costs { get; set; } = new();
    }

    /// <summary>
    /// Capabilities of an audio provider for routing decisions.
    /// </summary>
    public class AudioProviderRoutingCapabilities
    {
        /// <summary>
        /// Gets or sets whether streaming is supported.
        /// </summary>
        public bool SupportsStreaming { get; set; }

        /// <summary>
        /// Gets or sets the supported languages (ISO 639-1 codes).
        /// </summary>
        public List<string> SupportedLanguages { get; set; } = new();

        /// <summary>
        /// Gets or sets the supported audio formats.
        /// </summary>
        public List<string> SupportedFormats { get; set; } = new();

        /// <summary>
        /// Gets or sets the maximum audio duration in seconds.
        /// </summary>
        public int MaxAudioDurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets whether real-time processing is supported.
        /// </summary>
        public bool SupportsRealtime { get; set; }

        /// <summary>
        /// Gets or sets the supported voice IDs for TTS.
        /// </summary>
        public List<string> SupportedVoices { get; set; } = new();

        /// <summary>
        /// Gets or sets whether custom vocabulary is supported.
        /// </summary>
        public bool SupportsCustomVocabulary { get; set; }

        /// <summary>
        /// Gets or sets the quality score (0-100).
        /// </summary>
        public double QualityScore { get; set; }
    }

    /// <summary>
    /// Current performance metrics for an audio provider.
    /// </summary>
    public class AudioProviderMetrics
    {
        /// <summary>
        /// Gets or sets the average latency in milliseconds.
        /// </summary>
        public double AverageLatencyMs { get; set; }

        /// <summary>
        /// Gets or sets the 95th percentile latency.
        /// </summary>
        public double P95LatencyMs { get; set; }

        /// <summary>
        /// Gets or sets the success rate (0-1).
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Gets or sets the current load (0-1).
        /// </summary>
        public double CurrentLoad { get; set; }

        /// <summary>
        /// Gets or sets when metrics were last updated.
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Gets or sets the number of requests in the sample.
        /// </summary>
        public int SampleSize { get; set; }
    }

    /// <summary>
    /// Cost information for an audio provider.
    /// </summary>
    public class AudioProviderCosts
    {
        /// <summary>
        /// Gets or sets the cost per minute for STT.
        /// </summary>
        public decimal TranscriptionPerMinute { get; set; }

        /// <summary>
        /// Gets or sets the cost per 1000 characters for TTS.
        /// </summary>
        public decimal TextToSpeechPer1kChars { get; set; }

        /// <summary>
        /// Gets or sets the cost per minute for real-time audio.
        /// </summary>
        public decimal RealtimePerMinute { get; set; }
    }

    /// <summary>
    /// Metrics from an audio request.
    /// </summary>
    public class AudioRequestMetrics
    {
        /// <summary>
        /// Gets or sets the request type.
        /// </summary>
        public AudioRequestType RequestType { get; set; }

        /// <summary>
        /// Gets or sets the total latency in milliseconds.
        /// </summary>
        public double LatencyMs { get; set; }

        /// <summary>
        /// Gets or sets whether the request succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error code if failed.
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Gets or sets the audio duration in seconds.
        /// </summary>
        public double? DurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the character count (for TTS).
        /// </summary>
        public int? CharacterCount { get; set; }

        /// <summary>
        /// Gets or sets the language used.
        /// </summary>
        public string? Language { get; set; }

        /// <summary>
        /// Gets or sets when the request occurred.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Type of audio request.
    /// </summary>
    public enum AudioRequestType
    {
        /// <summary>
        /// Speech-to-text transcription.
        /// </summary>
        Transcription,

        /// <summary>
        /// Text-to-speech synthesis.
        /// </summary>
        TextToSpeech,

        /// <summary>
        /// Real-time audio conversation.
        /// </summary>
        Realtime
    }
}
