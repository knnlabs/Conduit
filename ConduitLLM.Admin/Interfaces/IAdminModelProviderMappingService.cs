using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.Admin.Interfaces;

/// <summary>
/// Service interface for managing model provider mappings through the Admin API
/// </summary>
public interface IAdminModelProviderMappingService
{
    /// <summary>
    /// Gets all model provider mappings
    /// </summary>
    /// <returns>List of all model provider mappings</returns>
    Task<IEnumerable<ModelProviderMappingDto>> GetAllMappingsAsync();

    /// <summary>
    /// Gets a model provider mapping by ID
    /// </summary>
    /// <param name="id">The ID of the mapping to retrieve</param>
    /// <returns>The model provider mapping, or null if not found</returns>
    Task<ModelProviderMappingDto?> GetMappingByIdAsync(int id);

    /// <summary>
    /// Gets a model provider mapping by model ID
    /// </summary>
    /// <param name="modelId">The model ID to look up</param>
    /// <returns>The model provider mapping, or null if not found</returns>
    Task<ModelProviderMappingDto?> GetMappingByModelIdAsync(string modelId);

    /// <summary>
    /// Adds a new model provider mapping
    /// </summary>
    /// <param name="mapping">The mapping to add</param>
    /// <returns>True if the operation was successful</returns>
    Task<bool> AddMappingAsync(ModelProviderMappingDto mapping);

    /// <summary>
    /// Updates an existing model provider mapping
    /// </summary>
    /// <param name="mapping">The mapping to update</param>
    /// <returns>True if the operation was successful</returns>
    Task<bool> UpdateMappingAsync(ModelProviderMappingDto mapping);

    /// <summary>
    /// Deletes a model provider mapping
    /// </summary>
    /// <param name="id">The ID of the mapping to delete</param>
    /// <returns>True if the operation was successful</returns>
    Task<bool> DeleteMappingAsync(int id);

    /// <summary>
    /// Gets a list of all available providers
    /// </summary>
    /// <returns>List of provider data with IDs and names</returns>
    Task<IEnumerable<ProviderDataDto>> GetProvidersAsync();
}
