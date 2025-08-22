using ConduitLLM.Core.Models.Audio;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for filtering inappropriate content in audio operations.
    /// </summary>
    public interface IAudioContentFilter
    {
        /// <summary>
        /// Filters transcribed text for inappropriate content.
        /// </summary>
        /// <param name="text">The transcribed text to filter.</param>
        /// <param name="virtualKey">The virtual key for tracking.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The filtered content result.</returns>
        Task<ContentFilterResult> FilterTranscriptionAsync(
            string text,
            string virtualKey,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Filters text before converting to speech.
        /// </summary>
        /// <param name="text">The text to filter before TTS.</param>
        /// <param name="virtualKey">The virtual key for tracking.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The filtered content result.</returns>
        Task<ContentFilterResult> FilterTextToSpeechAsync(
            string text,
            string virtualKey,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates audio content for inappropriate material.
        /// </summary>
        /// <param name="audioData">The audio data to validate.</param>
        /// <param name="format">The audio format.</param>
        /// <param name="virtualKey">The virtual key for tracking.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if content is appropriate, false otherwise.</returns>
        Task<bool> ValidateAudioContentAsync(
            byte[] audioData,
            AudioFormat format,
            string virtualKey,
            CancellationToken cancellationToken = default);
    }
}
