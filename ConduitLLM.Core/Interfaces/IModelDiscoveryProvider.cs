using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for discovering models and their capabilities from LLM providers.
    /// Enables dynamic model discovery from provider APIs where available.
    /// </summary>
    public interface IModelDiscoveryProvider
    {
        /// <summary>
        /// The name of the provider this discovery service supports.
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Indicates whether this provider supports dynamic model discovery.
        /// </summary>
        bool SupportsDiscovery { get; }

        /// <summary>
        /// Discovers all available models from the provider's API.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>List of discovered models with their metadata and capabilities.</returns>
        Task<List<ModelMetadata>> DiscoverModelsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets detailed information about a specific model from the provider's API.
        /// </summary>
        /// <param name="modelId">The ID of the model to get information for.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Model metadata if found, null otherwise.</returns>
        Task<ModelMetadata?> GetModelMetadataAsync(string modelId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tests whether the provider's discovery API is currently accessible.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>True if the API is accessible, false otherwise.</returns>
        Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Comprehensive metadata about a model including capabilities, pricing, and limits.
    /// </summary>
    public class ModelMetadata
    {
        /// <summary>
        /// The unique identifier for the model.
        /// </summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable display name for the model.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// The provider that owns this model.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Detailed capabilities of the model.
        /// </summary>
        public ModelCapabilities Capabilities { get; set; } = new();

        /// <summary>
        /// Maximum number of tokens the model can process in context.
        /// </summary>
        public int? MaxContextTokens { get; set; }

        /// <summary>
        /// Maximum number of tokens the model can generate in a single response.
        /// </summary>
        public int? MaxOutputTokens { get; set; }

        /// <summary>
        /// Cost per 1000 input tokens in USD.
        /// </summary>
        public decimal? InputTokenCost { get; set; }

        /// <summary>
        /// Cost per 1000 output tokens in USD.
        /// </summary>
        public decimal? OutputTokenCost { get; set; }

        /// <summary>
        /// Cost per image for image generation models.
        /// </summary>
        public decimal? ImageCostPerImage { get; set; }

        /// <summary>
        /// Supported image sizes for image generation models.
        /// </summary>
        public List<string> SupportedImageSizes { get; set; } = new();

        /// <summary>
        /// Supported video resolutions for video generation models.
        /// </summary>
        public List<string> SupportedVideoResolutions { get; set; } = new();

        /// <summary>
        /// Maximum video duration in seconds for video generation models.
        /// </summary>
        public int? MaxVideoDurationSeconds { get; set; }

        /// <summary>
        /// When this metadata was last updated from the provider.
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// How this model metadata was obtained.
        /// </summary>
        public ModelDiscoverySource Source { get; set; }

        /// <summary>
        /// Additional metadata from the provider that doesn't fit standard fields.
        /// </summary>
        public Dictionary<string, object> AdditionalMetadata { get; set; } = new();

        /// <summary>
        /// Any warnings or notes about this model's capabilities or limitations.
        /// </summary>
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Indicates how model metadata was discovered.
    /// </summary>
    public enum ModelDiscoverySource
    {
        /// <summary>
        /// Retrieved from the provider's discovery API.
        /// </summary>
        ProviderApi,

        /// <summary>
        /// Inferred from hardcoded patterns and rules.
        /// </summary>
        HardcodedPattern,

        /// <summary>
        /// Manually configured by an administrator.
        /// </summary>
        ManualOverride,

        /// <summary>
        /// Derived from model mappings in the database.
        /// </summary>
        DatabaseMapping
    }
}