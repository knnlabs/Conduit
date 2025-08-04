using ConduitLLM.Configuration.Entities;

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
}
