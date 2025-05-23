using ConfigDTO = ConduitLLM.Configuration.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Interfaces
{
    /// <summary>
    /// Interface for managing model provider mappings
    /// </summary>
    public interface IModelProviderMappingService
    {
        /// <summary>
        /// Gets all model provider mappings
        /// </summary>
        /// <returns>List of all model provider mappings</returns>
        Task<IEnumerable<ConfigDTO.ModelProviderMappingDto>> GetAllAsync();

        /// <summary>
        /// Gets a model provider mapping by ID
        /// </summary>
        /// <param name="id">The ID of the mapping to retrieve</param>
        /// <returns>The model provider mapping, or null if not found</returns>
        Task<ConfigDTO.ModelProviderMappingDto?> GetByIdAsync(int id);

        /// <summary>
        /// Gets a model provider mapping by model ID
        /// </summary>
        /// <param name="modelId">The model ID to look up</param>
        /// <returns>The model provider mapping, or null if not found</returns>
        Task<ConfigDTO.ModelProviderMappingDto?> GetByModelIdAsync(string modelId);

        /// <summary>
        /// Creates a new model provider mapping
        /// </summary>
        /// <param name="mapping">The mapping to create</param>
        /// <returns>The created mapping, or null if creation failed</returns>
        Task<ConfigDTO.ModelProviderMappingDto?> CreateAsync(ConfigDTO.ModelProviderMappingDto mapping);

        /// <summary>
        /// Updates an existing model provider mapping
        /// </summary>
        /// <param name="mapping">The mapping to update</param>
        /// <returns>The updated mapping, or null if update failed</returns>
        Task<ConfigDTO.ModelProviderMappingDto?> UpdateAsync(ConfigDTO.ModelProviderMappingDto mapping);

        /// <summary>
        /// Deletes a model provider mapping
        /// </summary>
        /// <param name="id">The ID of the mapping to delete</param>
        /// <returns>True if the deletion was successful, false otherwise</returns>
        Task<bool> DeleteAsync(int id);

        /// <summary>
        /// Gets a list of all available providers
        /// </summary>
        /// <returns>List of provider data with IDs and names</returns>
        Task<IEnumerable<ConfigDTO.ProviderDataDto>> GetProvidersAsync();
    }
}