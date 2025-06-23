using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Service for retrieving model capabilities from configuration.
    /// Replaces hardcoded model capability detection with database-driven configuration.
    /// </summary>
    public interface IModelCapabilityService
    {
        /// <summary>
        /// Determines if a model supports vision/image inputs.
        /// </summary>
        /// <param name="model">The model identifier to check.</param>
        /// <returns>True if the model supports vision inputs, false otherwise.</returns>
        Task<bool> SupportsVisionAsync(string model);

        /// <summary>
        /// Determines if a model supports audio transcription (Speech-to-Text).
        /// </summary>
        /// <param name="model">The model identifier to check.</param>
        /// <returns>True if the model supports audio transcription, false otherwise.</returns>
        Task<bool> SupportsAudioTranscriptionAsync(string model);

        /// <summary>
        /// Determines if a model supports text-to-speech generation.
        /// </summary>
        /// <param name="model">The model identifier to check.</param>
        /// <returns>True if the model supports TTS, false otherwise.</returns>
        Task<bool> SupportsTextToSpeechAsync(string model);

        /// <summary>
        /// Determines if a model supports real-time audio streaming.
        /// </summary>
        /// <param name="model">The model identifier to check.</param>
        /// <returns>True if the model supports real-time audio, false otherwise.</returns>
        Task<bool> SupportsRealtimeAudioAsync(string model);

        /// <summary>
        /// Determines if a model supports video generation.
        /// </summary>
        /// <param name="model">The model identifier to check.</param>
        /// <returns>True if the model supports video generation, false otherwise.</returns>
        Task<bool> SupportsVideoGenerationAsync(string model);

        /// <summary>
        /// Gets the tokenizer type for a model.
        /// </summary>
        /// <param name="model">The model identifier.</param>
        /// <returns>The tokenizer type (e.g., "cl100k_base", "p50k_base", "claude") or null if not specified.</returns>
        Task<string?> GetTokenizerTypeAsync(string model);

        /// <summary>
        /// Gets the list of supported voices for a TTS model.
        /// </summary>
        /// <param name="model">The model identifier.</param>
        /// <returns>List of supported voice identifiers.</returns>
        Task<List<string>> GetSupportedVoicesAsync(string model);

        /// <summary>
        /// Gets the list of supported languages for a model.
        /// </summary>
        /// <param name="model">The model identifier.</param>
        /// <returns>List of supported language codes.</returns>
        Task<List<string>> GetSupportedLanguagesAsync(string model);

        /// <summary>
        /// Gets the list of supported audio formats for a model.
        /// </summary>
        /// <param name="model">The model identifier.</param>
        /// <returns>List of supported audio format identifiers.</returns>
        Task<List<string>> GetSupportedFormatsAsync(string model);

        /// <summary>
        /// Gets the default model for a specific provider and capability type.
        /// </summary>
        /// <param name="provider">The provider name (e.g., "openai", "anthropic").</param>
        /// <param name="capabilityType">The capability type (e.g., "chat", "transcription", "tts", "realtime").</param>
        /// <returns>The default model identifier or null if no default is configured.</returns>
        Task<string?> GetDefaultModelAsync(string provider, string capabilityType);

        /// <summary>
        /// Refreshes the cached model capabilities.
        /// Should be called when model configurations are updated.
        /// </summary>
        Task RefreshCacheAsync();
    }
}
