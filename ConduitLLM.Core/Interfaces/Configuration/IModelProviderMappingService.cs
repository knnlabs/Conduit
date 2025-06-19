namespace ConduitLLM.Core.Interfaces.Configuration
{
    /// <summary>
    /// Service interface for managing model-to-provider mappings.
    /// </summary>
    public interface IModelProviderMappingService
    {
        /// <summary>
        /// Retrieves all model mappings.
        /// </summary>
        Task<List<ModelProviderMapping>> GetAllMappingsAsync();

        /// <summary>
        /// Retrieves a mapping by model alias.
        /// </summary>
        /// <param name="modelAlias">The model alias to search for.</param>
        /// <returns>The mapping if found, otherwise null.</returns>
        Task<ModelProviderMapping?> GetMappingByModelAliasAsync(string modelAlias);
    }

    /// <summary>
    /// Represents a mapping between a model alias and a provider's specific model.
    /// </summary>
    public class ModelProviderMapping
    {
        /// <summary>
        /// User-friendly model alias used in client requests.
        /// </summary>
        public string ModelAlias { get; set; } = string.Empty;

        /// <summary>
        /// The name of the provider.
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        /// The actual model identifier expected by the provider.
        /// </summary>
        public string ProviderModelId { get; set; } = string.Empty;

        /// <summary>
        /// Optional deployment name for providers that support deployments.
        /// </summary>
        public string? DeploymentName { get; set; }

        /// <summary>
        /// Whether this mapping is enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// The maximum number of tokens the model's context window can handle.
        /// </summary>
        public int? MaxContextTokens { get; set; }
        
        /// <summary>
        /// Indicates whether this model supports image generation.
        /// </summary>
        public bool SupportsImageGeneration { get; set; }
    }
}
