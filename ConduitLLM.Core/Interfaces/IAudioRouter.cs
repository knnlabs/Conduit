using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Models.Audio;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for routing audio requests to appropriate providers based on capabilities and availability.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The IAudioRouter provides intelligent routing for audio operations across multiple providers.
    /// It considers factors such as:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Provider capabilities (which operations they support)</description></item>
    /// <item><description>Model availability (specific models for transcription or TTS)</description></item>
    /// <item><description>Voice availability (for TTS operations)</description></item>
    /// <item><description>Language support</description></item>
    /// <item><description>Cost optimization</description></item>
    /// <item><description>Provider health and availability</description></item>
    /// </list>
    /// </remarks>
    public interface IAudioRouter
    {
        /// <summary>
        /// Gets an audio transcription client based on the request requirements.
        /// </summary>
        /// <param name="request">The transcription request with requirements.</param>
        /// <param name="virtualKey">The virtual key for authentication and routing rules.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>An appropriate transcription client, or null if none available.</returns>
        /// <remarks>
        /// <para>
        /// The router will consider:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Requested model (e.g., "whisper-1")</description></item>
        /// <item><description>Language requirements</description></item>
        /// <item><description>Audio format support</description></item>
        /// <item><description>Provider availability</description></item>
        /// </list>
        /// </remarks>
        Task<IAudioTranscriptionClient?> GetTranscriptionClientAsync(
            AudioTranscriptionRequest request,
            string virtualKey,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a text-to-speech client based on the request requirements.
        /// </summary>
        /// <param name="request">The TTS request with requirements.</param>
        /// <param name="virtualKey">The virtual key for authentication and routing rules.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>An appropriate TTS client, or null if none available.</returns>
        /// <remarks>
        /// <para>
        /// The router will consider:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Requested voice availability</description></item>
        /// <item><description>Model preferences (e.g., "tts-1" vs "tts-1-hd")</description></item>
        /// <item><description>Language support</description></item>
        /// <item><description>Audio format requirements</description></item>
        /// <item><description>Streaming capability needs</description></item>
        /// </list>
        /// </remarks>
        Task<ITextToSpeechClient?> GetTextToSpeechClientAsync(
            TextToSpeechRequest request,
            string virtualKey,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a real-time audio client based on the session configuration.
        /// </summary>
        /// <param name="config">The real-time session configuration.</param>
        /// <param name="virtualKey">The virtual key for authentication and routing rules.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>An appropriate real-time client, or null if none available.</returns>
        /// <remarks>
        /// <para>
        /// Real-time routing is more complex as it considers:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Provider support for real-time conversations</description></item>
        /// <item><description>Model availability (e.g., "gpt-4o-realtime")</description></item>
        /// <item><description>Voice preferences</description></item>
        /// <item><description>Function calling requirements</description></item>
        /// <item><description>Latency requirements</description></item>
        /// </list>
        /// </remarks>
        Task<IRealtimeAudioClient?> GetRealtimeClientAsync(
            RealtimeSessionConfig config,
            string virtualKey,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all available transcription providers for a virtual key.
        /// </summary>
        /// <param name="virtualKey">The virtual key to check access for.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>List of provider names that support transcription.</returns>
        Task<List<string>> GetAvailableTranscriptionProvidersAsync(
            string virtualKey,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all available TTS providers for a virtual key.
        /// </summary>
        /// <param name="virtualKey">The virtual key to check access for.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>List of provider names that support TTS.</returns>
        Task<List<string>> GetAvailableTextToSpeechProvidersAsync(
            string virtualKey,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all available real-time providers for a virtual key.
        /// </summary>
        /// <param name="virtualKey">The virtual key to check access for.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>List of provider names that support real-time audio.</returns>
        Task<List<string>> GetAvailableRealtimeProvidersAsync(
            string virtualKey,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates that a specific audio operation can be performed.
        /// </summary>
        /// <param name="operation">The type of audio operation.</param>
        /// <param name="provider">The provider to validate.</param>
        /// <param name="request">The request to validate.</param>
        /// <param name="errorMessage">Error message if validation fails.</param>
        /// <returns>True if the operation can be performed, false otherwise.</returns>
        bool ValidateAudioOperation(
            AudioOperation operation,
            string provider,
            AudioRequestBase request,
            out string errorMessage);

        /// <summary>
        /// Gets routing statistics for audio operations.
        /// </summary>
        /// <param name="virtualKey">The virtual key to get statistics for.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Statistics about audio routing decisions.</returns>
        Task<AudioRoutingStatistics> GetRoutingStatisticsAsync(
            string virtualKey,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Statistics about audio routing decisions.
    /// </summary>
    public class AudioRoutingStatistics
    {
        /// <summary>
        /// Total number of transcription requests routed.
        /// </summary>
        public long TranscriptionRequests { get; set; }

        /// <summary>
        /// Total number of TTS requests routed.
        /// </summary>
        public long TextToSpeechRequests { get; set; }

        /// <summary>
        /// Total number of real-time sessions routed.
        /// </summary>
        public long RealtimeSessions { get; set; }

        /// <summary>
        /// Provider usage breakdown.
        /// </summary>
        public Dictionary<string, ProviderAudioStats> ProviderStats { get; set; } = new();

        /// <summary>
        /// Failed routing attempts.
        /// </summary>
        public long FailedRoutingAttempts { get; set; }

        /// <summary>
        /// Average routing decision time in milliseconds.
        /// </summary>
        public double AverageRoutingTimeMs { get; set; }
    }

    /// <summary>
    /// Audio statistics for a specific provider.
    /// </summary>
    public class ProviderAudioStats
    {
        /// <summary>
        /// Number of transcription requests handled.
        /// </summary>
        public long TranscriptionCount { get; set; }

        /// <summary>
        /// Number of TTS requests handled.
        /// </summary>
        public long TextToSpeechCount { get; set; }

        /// <summary>
        /// Number of real-time sessions handled.
        /// </summary>
        public long RealtimeCount { get; set; }

        /// <summary>
        /// Total audio minutes processed.
        /// </summary>
        public double TotalAudioMinutes { get; set; }

        /// <summary>
        /// Total characters synthesized.
        /// </summary>
        public long TotalCharactersSynthesized { get; set; }

        /// <summary>
        /// Success rate percentage.
        /// </summary>
        public double SuccessRate { get; set; }
    }
}