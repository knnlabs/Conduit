using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Audio;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Provides hybrid audio conversation capabilities by chaining STT, LLM, and TTS services.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The IHybridAudioService interface enables conversational AI experiences for providers
    /// that don't have native real-time audio support. It accomplishes this by:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Converting speech to text using an STT provider</description></item>
    /// <item><description>Processing the text through an LLM for response generation</description></item>
    /// <item><description>Converting the response back to speech using a TTS provider</description></item>
    /// </list>
    /// <para>
    /// This service is designed to minimize latency while providing a seamless audio
    /// conversation experience, with support for interruptions and context management.
    /// </para>
    /// </remarks>
    public interface IHybridAudioService
    {
        /// <summary>
        /// Processes a single audio input through the STT-LLM-TTS pipeline.
        /// </summary>
        /// <param name="request">The hybrid audio request containing audio data and configuration.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The response containing the generated audio and metadata.</returns>
        /// <exception cref="Exceptions.ValidationException">Thrown when the request fails validation.</exception>
        /// <exception cref="Exceptions.LLMCommunicationException">Thrown when there is an error in any pipeline stage.</exception>
        /// <remarks>
        /// <para>
        /// This method orchestrates the complete pipeline:
        /// </para>
        /// <list type="number">
        /// <item><description>Transcribes the input audio to text</description></item>
        /// <item><description>Sends the text to the LLM with conversation context</description></item>
        /// <item><description>Converts the LLM response to speech</description></item>
        /// </list>
        /// <para>
        /// The response includes both the generated audio and intermediate results
        /// (transcription and LLM response text) for debugging and logging purposes.
        /// </para>
        /// </remarks>
        Task<HybridAudioResponse> ProcessAudioAsync(
            HybridAudioRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Processes audio input with streaming output for lower latency.
        /// </summary>
        /// <param name="request">The hybrid audio request containing audio data and configuration.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>An async enumerable of response chunks as they are generated.</returns>
        /// <exception cref="Exceptions.ValidationException">Thrown when the request fails validation.</exception>
        /// <exception cref="Exceptions.LLMCommunicationException">Thrown when there is an error in any pipeline stage.</exception>
        /// <remarks>
        /// <para>
        /// This streaming version provides lower latency by:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Streaming LLM tokens as they are generated</description></item>
        /// <item><description>Starting TTS synthesis before the complete response is available</description></item>
        /// <item><description>Yielding audio chunks progressively for immediate playback</description></item>
        /// </list>
        /// <para>
        /// Each chunk contains partial audio data and metadata about the generation progress.
        /// </para>
        /// </remarks>
        IAsyncEnumerable<HybridAudioChunk> StreamProcessAudioAsync(
            HybridAudioRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new conversation session for managing context across multiple interactions.
        /// </summary>
        /// <param name="config">Configuration for the conversation session.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A session ID for tracking the conversation.</returns>
        /// <remarks>
        /// <para>
        /// Sessions maintain conversation history and context, enabling multi-turn
        /// conversations with consistent personality and memory of previous interactions.
        /// </para>
        /// <para>
        /// Sessions should be explicitly closed when no longer needed to free resources.
        /// </para>
        /// </remarks>
        Task<string> CreateSessionAsync(
            HybridSessionConfig config,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes an active conversation session and releases associated resources.
        /// </summary>
        /// <param name="sessionId">The ID of the session to close.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// This method cleans up session state and ensures any pending operations
        /// are completed or cancelled appropriately.
        /// </remarks>
        Task CloseSessionAsync(
            string sessionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if the hybrid audio service is available with the current configuration.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>True if all required services (STT, LLM, TTS) are available.</returns>
        /// <remarks>
        /// <para>
        /// This method verifies that:
        /// </para>
        /// <list type="bullet">
        /// <item><description>An STT provider is configured and available</description></item>
        /// <item><description>An LLM provider is configured and available</description></item>
        /// <item><description>A TTS provider is configured and available</description></item>
        /// </list>
        /// </remarks>
        Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current latency metrics for the hybrid pipeline.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>Latency information for each pipeline stage.</returns>
        /// <remarks>
        /// <para>
        /// Returns timing information that can be used to optimize the pipeline,
        /// including average latencies for:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Speech-to-text transcription</description></item>
        /// <item><description>LLM response generation</description></item>
        /// <item><description>Text-to-speech synthesis</description></item>
        /// <item><description>Total end-to-end processing</description></item>
        /// </list>
        /// </remarks>
        Task<HybridLatencyMetrics> GetLatencyMetricsAsync(
            CancellationToken cancellationToken = default);
    }
}
