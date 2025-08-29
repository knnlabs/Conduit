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
        /// Gets the default model for a specific provider and capability type.
        /// </summary>
        /// <param name="provider">The provider name (e.g., "openai", "anthropic").</param>
        /// <param name="capabilityType">The capability type (e.g., "chat", "vision", "embeddings").</param>
        /// <returns>The default model identifier or null if no default is configured.</returns>
        Task<string?> GetDefaultModelAsync(string provider, string capabilityType);

        /// <summary>
        /// Refreshes the cached model capabilities.
        /// Should be called when model configurations are updated.
        /// </summary>
        Task RefreshCacheAsync();
    }
}
