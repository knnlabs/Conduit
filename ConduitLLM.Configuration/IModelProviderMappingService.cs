using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration
{
    /// <summary>
    /// Interface for managing model-provider mappings.
    /// </summary>
    public interface IModelProviderMappingService
    {
        Task<Entities.ModelProviderMapping?> GetMappingByIdAsync(int id);
        Task<List<Entities.ModelProviderMapping>> GetAllMappingsAsync();
        Task AddMappingAsync(Entities.ModelProviderMapping mapping);
        Task UpdateMappingAsync(Entities.ModelProviderMapping mapping);
        Task DeleteMappingAsync(int id);
        Task<Entities.ModelProviderMapping?> GetMappingByModelAliasAsync(string modelAlias);

        /// <summary>
        /// Validates and creates a new model provider mapping
        /// </summary>
        /// <param name="mapping">The mapping to create</param>
        /// <returns>Validation result with created mapping if successful</returns>
        Task<(bool success, string? errorMessage, Entities.ModelProviderMapping? createdMapping)> ValidateAndCreateMappingAsync(Entities.ModelProviderMapping mapping);

        /// <summary>
        /// Validates and updates an existing model provider mapping
        /// </summary>
        /// <param name="id">The ID of the mapping to update</param>
        /// <param name="mapping">The updated mapping data</param>
        /// <returns>Validation result</returns>
        Task<(bool success, string? errorMessage)> ValidateAndUpdateMappingAsync(int id, Entities.ModelProviderMapping mapping);

        /// <summary>
        /// Validates that a provider exists by ID
        /// </summary>
        /// <param name="providerId">The provider ID to validate</param>
        /// <returns>True if the provider exists, false otherwise</returns>
        Task<bool> ProviderExistsByIdAsync(int providerId);

        /// <summary>
        /// Gets a list of all available providers
        /// </summary>
        /// <returns>List of available providers with their IDs and names</returns>
        Task<List<(int Id, string ProviderName)>> GetAvailableProvidersAsync();
    }
}
