using ConduitLLM.Core.Models.Audio;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Optional capability: text-to-speech synthesis. Implemented by provider clients that support an
    /// <c>/audio/speech</c> endpoint and discovered via
    /// <see cref="LLMClientDecoratorExtensions.FindInChain{T}"/> (it is not part of <see cref="ILLMClient"/>).
    /// </summary>
    public interface ITextToSpeechClient
    {
        /// <summary>
        /// Synthesizes speech audio from the supplied text.
        /// </summary>
        Task<TextToSpeechResponse> CreateSpeechAsync(
            TextToSpeechRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default);
    }
}
