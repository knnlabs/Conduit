using System.Threading;
using System.Threading.Tasks;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Simplified audio router interface for internal use.
    /// </summary>
    /// <remarks>
    /// This interface provides a simpler routing mechanism that doesn't require
    /// full request objects, suitable for internal service implementations.
    /// </remarks>
    public interface ISimpleAudioRouter
    {
        /// <summary>
        /// Gets an audio transcription client based on language preference.
        /// </summary>
        /// <param name="language">Optional language preference.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An appropriate transcription client, or null if none available.</returns>
        Task<IAudioTranscriptionClient?> GetTranscriptionClientAsync(
            string? language = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a text-to-speech client based on voice preference.
        /// </summary>
        /// <param name="voice">Optional voice preference.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An appropriate TTS client, or null if none available.</returns>
        Task<ITextToSpeechClient?> GetTextToSpeechClientAsync(
            string? voice = null,
            CancellationToken cancellationToken = default);
    }
}
