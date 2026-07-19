using ConduitLLM.Core.Models.Audio;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Optional capability: speech-to-text transcription. Implemented by provider clients that support
    /// an <c>/audio/transcriptions</c> endpoint and discovered via
    /// <see cref="LLMClientDecoratorExtensions.FindInChain{T}"/> (it is not part of <see cref="ILLMClient"/>).
    /// </summary>
    public interface IAudioTranscriptionClient
    {
        /// <summary>
        /// Transcribes the supplied audio to text.
        /// </summary>
        Task<AudioTranscriptionResponse> TranscribeAudioAsync(
            AudioTranscriptionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default);
    }
}
