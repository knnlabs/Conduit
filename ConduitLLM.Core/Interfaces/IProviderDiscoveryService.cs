using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Service for discovering provider capabilities and available models.
    /// </summary>
    public interface IProviderDiscoveryService
    {
        /// <summary>
        /// Discovers all available models and their capabilities from configured providers.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A dictionary mapping model names to their capabilities.</returns>
        Task<Dictionary<string, DiscoveredModel>> DiscoverModelsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Discovers capabilities for a specific provider instance.
        /// </summary>
        /// <param name="Provider">The provider credential containing configuration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A dictionary mapping model names to their capabilities for the specified provider.</returns>
        Task<Dictionary<string, DiscoveredModel>> DiscoverProviderModelsAsync(
            Provider Provider, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Tests if a specific model supports a given capability.
        /// </summary>
        /// <param name="modelName">The model name to test.</param>
        /// <param name="capability">The capability to check.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the model supports the capability.</returns>
        Task<bool> TestModelCapabilityAsync(
            string modelName, 
            ModelCapability capability, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the cached capabilities for all configured providers.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task representing the async operation.</returns>
        Task RefreshCapabilitiesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the cached capabilities for a specific provider.
        /// This method is called when provider credentials are updated to refresh the cached model capabilities.
        /// </summary>
        /// <param name="providerId">The ID of the provider to refresh.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task representing the async operation.</returns>
        Task RefreshProviderCapabilitiesAsync(int providerId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents a discovered model with its capabilities.
    /// </summary>
    public class DiscoveredModel
    {
        /// <summary>
        /// The model identifier.
        /// </summary>
        public required string ModelId { get; set; }

        /// <summary>
        /// The provider that hosts this model.
        /// </summary>
        public required string Provider { get; set; }

        /// <summary>
        /// Display name for the model.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Model capabilities.
        /// </summary>
        public required ModelCapabilities Capabilities { get; set; }

        /// <summary>
        /// Additional metadata about the model.
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }

        /// <summary>
        /// When this model's capabilities were last verified.
        /// </summary>
        public DateTime LastVerified { get; set; }
    }

    /// <summary>
    /// Model capabilities that match the ILLMClient interface.
    /// </summary>
    public class ModelCapabilities
    {
        /// <summary>
        /// Supports chat completions.
        /// </summary>
        public bool Chat { get; set; }

        /// <summary>
        /// Supports streaming chat completions.
        /// </summary>
        public bool ChatStream { get; set; }

        /// <summary>
        /// Supports embeddings.
        /// </summary>
        public bool Embeddings { get; set; }

        /// <summary>
        /// Supports image generation.
        /// </summary>
        public bool ImageGeneration { get; set; }

        /// <summary>
        /// Supports vision/multimodal inputs.
        /// </summary>
        public bool Vision { get; set; }

        /// <summary>
        /// Supports video generation.
        /// </summary>
        public bool VideoGeneration { get; set; }

        /// <summary>
        /// Supports video understanding.
        /// </summary>
        public bool VideoUnderstanding { get; set; }

        /// <summary>
        /// Supports function calling.
        /// </summary>
        public bool FunctionCalling { get; set; }

        /// <summary>
        /// Supports tool use.
        /// </summary>
        public bool ToolUse { get; set; }

        /// <summary>
        /// Supports JSON mode.
        /// </summary>
        public bool JsonMode { get; set; }

        /// <summary>
        /// Maximum context length in tokens.
        /// </summary>
        public int? MaxTokens { get; set; }

        /// <summary>
        /// Maximum output tokens.
        /// </summary>
        public int? MaxOutputTokens { get; set; }

        /// <summary>
        /// Supported image sizes for generation.
        /// </summary>
        public List<string>? SupportedImageSizes { get; set; }

        /// <summary>
        /// Supported video resolutions.
        /// </summary>
        public List<string>? SupportedVideoResolutions { get; set; }

        /// <summary>
        /// Maximum video duration in seconds.
        /// </summary>
        public int? MaxVideoDurationSeconds { get; set; }
    }

    /// <summary>
    /// Specific model capabilities to test.
    /// </summary>
    public enum ModelCapability
    {
        Chat,
        ChatStream,
        Embeddings,
        ImageGeneration,
        Vision,
        VideoGeneration,
        VideoUnderstanding,
        FunctionCalling,
        ToolUse,
        JsonMode
    }
}