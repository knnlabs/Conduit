using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Models.Audio;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for audio transcription (Speech-to-Text) capabilities.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The IAudioTranscriptionClient interface provides a standardized way to convert
    /// audio content into text across different provider implementations. This includes
    /// support for various audio formats, languages, and transcription options.
    /// </para>
    /// <para>
    /// Implementations of this interface handle provider-specific details such as:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Audio format conversion and validation</description></item>
    /// <item><description>Language detection and specification</description></item>
    /// <item><description>Timestamp generation for words or segments</description></item>
    /// <item><description>Provider-specific parameter mappings</description></item>
    /// <item><description>Error handling and retry logic</description></item>
    /// </list>
    /// </remarks>
    public interface IAudioTranscriptionClient
    {
        /// <summary>
        /// Transcribes audio content into text.
        /// </summary>
        /// <param name="request">The transcription request containing audio data and parameters.</param>
        /// <param name="apiKey">Optional API key override to use instead of the client's configured key.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The transcription response containing the converted text and metadata.</returns>
        /// <exception cref="Core.Exceptions.ValidationException">Thrown when the request fails validation.</exception>
        /// <exception cref="Core.Exceptions.LLMCommunicationException">Thrown when there is an error communicating with the provider.</exception>
        /// <exception cref="System.NotSupportedException">Thrown when the provider does not support transcription.</exception>
        /// <remarks>
        /// <para>
        /// This method accepts audio in various formats (mp3, mp4, wav, etc.) and converts it to text.
        /// The audio can be provided as raw bytes or as a URL reference, depending on provider support.
        /// </para>
        /// <para>
        /// The response includes the transcribed text and may optionally include:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Detected language of the audio</description></item>
        /// <item><description>Word-level or segment-level timestamps</description></item>
        /// <item><description>Confidence scores for the transcription</description></item>
        /// <item><description>Alternative transcriptions</description></item>
        /// </list>
        /// </remarks>
        Task<AudioTranscriptionResponse> TranscribeAudioAsync(
            AudioTranscriptionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if the client supports audio transcription.
        /// </summary>
        /// <param name="apiKey">Optional API key override to use instead of the client's configured key.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>True if transcription is supported, false otherwise.</returns>
        /// <remarks>
        /// <para>
        /// This method allows callers to check transcription support before attempting
        /// to use the service. This is useful for graceful degradation and routing decisions.
        /// </para>
        /// <para>
        /// Support may vary based on the API key permissions or the specific model configured
        /// for the client instance.
        /// </para>
        /// </remarks>
        Task<bool> SupportsTranscriptionAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists the audio formats supported by this transcription client.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A list of supported audio format identifiers.</returns>
        /// <remarks>
        /// <para>
        /// Returns the audio formats that this provider can process, such as:
        /// mp3, mp4, mpeg, mpga, m4a, wav, webm, flac, ogg, etc.
        /// </para>
        /// <para>
        /// This information can be used to validate input formats or to convert
        /// audio to a supported format before transcription.
        /// </para>
        /// </remarks>
        Task<List<string>> GetSupportedFormatsAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists the languages supported by this transcription client.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A list of supported language codes (ISO 639-1 format).</returns>
        /// <remarks>
        /// <para>
        /// Returns the languages that this provider can transcribe, using standard
        /// ISO 639-1 language codes (e.g., "en", "es", "fr", "zh").
        /// </para>
        /// <para>
        /// Some providers may support automatic language detection, in which case
        /// the language parameter in the request can be omitted.
        /// </para>
        /// </remarks>
        Task<List<string>> GetSupportedLanguagesAsync(
            CancellationToken cancellationToken = default);
    }
}
