using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Models.Audio;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for text-to-speech (TTS) synthesis capabilities.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ITextToSpeechClient interface provides a standardized way to convert text
    /// into spoken audio across different provider implementations. This includes
    /// support for multiple voices, languages, audio formats, and speech parameters.
    /// </para>
    /// <para>
    /// Implementations of this interface handle provider-specific details such as:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Voice selection and customization</description></item>
    /// <item><description>Speech rate, pitch, and volume control</description></item>
    /// <item><description>Audio format encoding (MP3, WAV, etc.)</description></item>
    /// <item><description>SSML (Speech Synthesis Markup Language) support</description></item>
    /// <item><description>Streaming audio generation for long texts</description></item>
    /// </list>
    /// </remarks>
    public interface ITextToSpeechClient
    {
        /// <summary>
        /// Converts text into speech audio.
        /// </summary>
        /// <param name="request">The text-to-speech request containing the text and synthesis parameters.</param>
        /// <param name="apiKey">Optional API key override to use instead of the client's configured key.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The speech synthesis response containing the audio data.</returns>
        /// <exception cref="Core.Exceptions.ValidationException">Thrown when the request fails validation.</exception>
        /// <exception cref="Core.Exceptions.LLMCommunicationException">Thrown when there is an error communicating with the provider.</exception>
        /// <exception cref="System.NotSupportedException">Thrown when the provider does not support text-to-speech.</exception>
        /// <remarks>
        /// <para>
        /// This method synthesizes speech from the provided text using the specified voice
        /// and audio parameters. The response contains the complete audio data.
        /// </para>
        /// <para>
        /// For long texts or real-time applications, consider using the streaming version
        /// <see cref="StreamSpeechAsync"/> which provides audio chunks as they are generated.
        /// </para>
        /// </remarks>
        Task<TextToSpeechResponse> CreateSpeechAsync(
            TextToSpeechRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Converts text into speech audio with streaming output.
        /// </summary>
        /// <param name="request">The text-to-speech request containing the text and synthesis parameters.</param>
        /// <param name="apiKey">Optional API key override to use instead of the client's configured key.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>An async enumerable of audio chunks as they are generated.</returns>
        /// <exception cref="Core.Exceptions.ValidationException">Thrown when the request fails validation.</exception>
        /// <exception cref="Core.Exceptions.LLMCommunicationException">Thrown when there is an error communicating with the provider.</exception>
        /// <exception cref="System.NotSupportedException">Thrown when the provider does not support streaming text-to-speech.</exception>
        /// <remarks>
        /// <para>
        /// This method is similar to <see cref="CreateSpeechAsync"/> but returns audio
        /// data incrementally as it is generated. This enables:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Lower latency for first audio output</description></item>
        /// <item><description>Progressive playback while generation continues</description></item>
        /// <item><description>Memory-efficient processing of long texts</description></item>
        /// <item><description>Real-time audio streaming applications</description></item>
        /// </list>
        /// <para>
        /// Not all providers support streaming. Implementations should throw a
        /// <see cref="System.NotSupportedException"/> if streaming is not available.
        /// </para>
        /// </remarks>
        IAsyncEnumerable<AudioChunk> StreamSpeechAsync(
            TextToSpeechRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists the voices available from this text-to-speech provider.
        /// </summary>
        /// <param name="apiKey">Optional API key override to use instead of the client's configured key.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A list of available voices with their metadata.</returns>
        /// <exception cref="Core.Exceptions.LLMCommunicationException">Thrown when there is an error communicating with the provider.</exception>
        /// <remarks>
        /// <para>
        /// Returns detailed information about each available voice, including:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Voice ID for use in synthesis requests</description></item>
        /// <item><description>Display name and description</description></item>
        /// <item><description>Supported languages and accents</description></item>
        /// <item><description>Gender and age characteristics</description></item>
        /// <item><description>Voice style capabilities (e.g., emotional range)</description></item>
        /// </list>
        /// <para>
        /// Some providers may offer voice cloning or custom voices, which may appear
        /// in this list if the API key has appropriate permissions.
        /// </para>
        /// </remarks>
        Task<List<VoiceInfo>> ListVoicesAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the audio formats supported by this text-to-speech client.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A list of supported audio format identifiers.</returns>
        /// <remarks>
        /// <para>
        /// Returns the audio output formats that this provider can generate, such as:
        /// mp3, wav, flac, ogg, aac, opus, etc.
        /// </para>
        /// <para>
        /// Different formats may have different quality, compression, and compatibility
        /// characteristics. Choose based on your application's requirements.
        /// </para>
        /// </remarks>
        Task<List<string>> GetSupportedFormatsAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if the client supports text-to-speech synthesis.
        /// </summary>
        /// <param name="apiKey">Optional API key override to use instead of the client's configured key.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>True if text-to-speech is supported, false otherwise.</returns>
        /// <remarks>
        /// <para>
        /// This method allows callers to check TTS support before attempting
        /// to use the service. This is useful for graceful degradation and routing decisions.
        /// </para>
        /// <para>
        /// Support may vary based on the API key permissions or the specific model/deployment
        /// configured for the client instance.
        /// </para>
        /// </remarks>
        Task<bool> SupportsTextToSpeechAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default);
    }
}