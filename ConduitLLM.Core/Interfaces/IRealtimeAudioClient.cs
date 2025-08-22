using ConduitLLM.Core.Models.Audio;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for real-time conversational audio AI capabilities.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The IRealtimeAudioClient interface provides a standardized way to handle
    /// bidirectional audio streaming for conversational AI applications. This enables
    /// low-latency, natural conversations with AI models that can process audio input
    /// and generate audio responses in real-time.
    /// </para>
    /// <para>
    /// Key features supported by implementations:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Bidirectional audio streaming via WebSockets</description></item>
    /// <item><description>Voice activity detection (VAD) and turn management</description></item>
    /// <item><description>Interruption handling for natural conversations</description></item>
    /// <item><description>Function calling during audio conversations</description></item>
    /// <item><description>Multiple voice and persona options</description></item>
    /// <item><description>Real-time transcription of both user and AI speech</description></item>
    /// </list>
    /// <para>
    /// This interface abstracts provider-specific implementations from OpenAI Realtime API,
    /// Ultravox, ElevenLabs Conversational AI, and other emerging real-time AI platforms.
    /// </para>
    /// </remarks>
    public interface IRealtimeAudioClient
    {
        /// <summary>
        /// Creates a new real-time audio session with the AI provider.
        /// </summary>
        /// <param name="config">Configuration for the real-time session including voice, model, and behavior settings.</param>
        /// <param name="apiKey">Optional API key override to use instead of the client's configured key.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A session object representing the established connection.</returns>
        /// <exception cref="Core.Exceptions.ValidationException">Thrown when the configuration fails validation.</exception>
        /// <exception cref="Core.Exceptions.LLMCommunicationException">Thrown when there is an error establishing the connection.</exception>
        /// <exception cref="System.NotSupportedException">Thrown when the provider does not support real-time audio.</exception>
        /// <remarks>
        /// <para>
        /// This method establishes a WebSocket connection to the provider's real-time endpoint
        /// and configures the session with the specified parameters. The session must be
        /// properly disposed when no longer needed to close the connection.
        /// </para>
        /// <para>
        /// Configuration options vary by provider but typically include:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Model selection (e.g., gpt-4o-realtime)</description></item>
        /// <item><description>Voice selection for AI responses</description></item>
        /// <item><description>Turn detection settings (VAD parameters)</description></item>
        /// <item><description>System prompt for conversation context</description></item>
        /// <item><description>Function definitions for tool use</description></item>
        /// <item><description>Audio format specifications</description></item>
        /// </list>
        /// </remarks>
        Task<RealtimeSession> CreateSessionAsync(
            RealtimeSessionConfig config,
            string? apiKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts bidirectional audio streaming for the established session.
        /// </summary>
        /// <param name="session">The active real-time session to stream with.</param>
        /// <param name="cancellationToken">A token to cancel the streaming operation.</param>
        /// <returns>A duplex stream for sending and receiving audio/events.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the session is not in a valid state for streaming.</exception>
        /// <exception cref="Core.Exceptions.LLMCommunicationException">Thrown when there is an error during streaming.</exception>
        /// <remarks>
        /// <para>
        /// The returned duplex stream allows simultaneous sending of audio input and
        /// receiving of AI responses. The stream handles multiple event types:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Audio input frames from the user</description></item>
        /// <item><description>Audio output frames from the AI</description></item>
        /// <item><description>Transcription updates for both parties</description></item>
        /// <item><description>Turn start/end events</description></item>
        /// <item><description>Function call requests and responses</description></item>
        /// <item><description>Error and status events</description></item>
        /// </list>
        /// <para>
        /// The stream continues until explicitly closed or an error occurs. Proper
        /// error handling and reconnection logic should be implemented by callers.
        /// </para>
        /// </remarks>
        IAsyncDuplexStream<RealtimeAudioFrame, RealtimeResponse> StreamAudioAsync(
            RealtimeSession session,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the configuration of an active real-time session.
        /// </summary>
        /// <param name="session">The session to update.</param>
        /// <param name="updates">The configuration updates to apply.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task representing the update operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the session cannot be updated.</exception>
        /// <exception cref="Core.Exceptions.ValidationException">Thrown when the updates are invalid.</exception>
        /// <remarks>
        /// <para>
        /// Allows dynamic updates to session parameters without disconnecting, such as:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Changing the system prompt mid-conversation</description></item>
        /// <item><description>Updating voice settings</description></item>
        /// <item><description>Modifying turn detection parameters</description></item>
        /// <item><description>Adding or removing function definitions</description></item>
        /// </list>
        /// <para>
        /// Not all parameters may be updatable depending on the provider's capabilities.
        /// Some changes may only take effect for subsequent turns in the conversation.
        /// </para>
        /// </remarks>
        Task UpdateSessionAsync(
            RealtimeSession session,
            RealtimeSessionUpdate updates,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes an active real-time session.
        /// </summary>
        /// <param name="session">The session to close.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task representing the close operation.</returns>
        /// <remarks>
        /// <para>
        /// Properly closes the WebSocket connection and cleans up resources associated
        /// with the session. This method should always be called when a session is no
        /// longer needed, even if an error occurred during streaming.
        /// </para>
        /// <para>
        /// After closing, the session object should not be reused. A new session must
        /// be created for subsequent conversations.
        /// </para>
        /// </remarks>
        Task CloseSessionAsync(
            RealtimeSession session,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if the client supports real-time audio conversations.
        /// </summary>
        /// <param name="apiKey">Optional API key override to use instead of the client's configured key.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>True if real-time audio is supported, false otherwise.</returns>
        /// <remarks>
        /// <para>
        /// This method allows callers to check real-time support before attempting
        /// to create a session. Support may depend on:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Provider capabilities</description></item>
        /// <item><description>API key permissions</description></item>
        /// <item><description>Model availability</description></item>
        /// <item><description>Regional restrictions</description></item>
        /// </list>
        /// </remarks>
        Task<bool> SupportsRealtimeAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the capabilities and limitations of this real-time client.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>Detailed information about supported features.</returns>
        /// <remarks>
        /// <para>
        /// Returns information about provider-specific capabilities such as:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Supported audio formats and sample rates</description></item>
        /// <item><description>Available voices and their characteristics</description></item>
        /// <item><description>Turn detection options</description></item>
        /// <item><description>Function calling support</description></item>
        /// <item><description>Maximum session duration</description></item>
        /// <item><description>Concurrent session limits</description></item>
        /// </list>
        /// </remarks>
        Task<RealtimeCapabilities> GetCapabilitiesAsync(
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents a bidirectional stream for real-time audio communication.
    /// </summary>
    /// <typeparam name="TInput">The type of data sent to the stream.</typeparam>
    /// <typeparam name="TOutput">The type of data received from the stream.</typeparam>
    public interface IAsyncDuplexStream<TInput, TOutput>
    {
        /// <summary>
        /// Sends data to the stream.
        /// </summary>
        /// <param name="item">The data to send.</param>
        /// <param name="cancellationToken">A token to cancel the send operation.</param>
        /// <returns>A task representing the send operation.</returns>
        ValueTask SendAsync(TInput item, CancellationToken cancellationToken = default);

        /// <summary>
        /// Receives data from the stream.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the receive operation.</param>
        /// <returns>An async enumerable of received data.</returns>
        IAsyncEnumerable<TOutput> ReceiveAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Completes the input side of the stream, signaling no more data will be sent.
        /// </summary>
        /// <returns>A task representing the completion operation.</returns>
        ValueTask CompleteAsync();

        /// <summary>
        /// Gets whether the stream is still connected and operational.
        /// </summary>
        bool IsConnected { get; }
    }
}
