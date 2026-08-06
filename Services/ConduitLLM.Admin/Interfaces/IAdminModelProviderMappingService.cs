using ConduitLLM.Configuration.Entities;
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
    Task<IEnumerable<ModelProviderMapping>> GetAllMappingsAsync();

    /// <summary>
    /// Gets a model provider mapping by ID
    /// </summary>
    /// <param name="id">The ID of the mapping to retrieve</param>
    /// <returns>The model provider mapping, or null if not found</returns>
    Task<ModelProviderMapping?> GetMappingByIdAsync(int id);

    /// <summary>
    /// Gets a model provider mapping by model ID
    /// </summary>
    /// <param name="modelId">The model ID to look up</param>
    /// <returns>The model provider mapping, or null if not found</returns>
    Task<ModelProviderMapping?> GetMappingByModelIdAsync(int modelId);

    /// <summary>
    /// Gets all model provider mappings for a specific model
    /// </summary>
    /// <param name="modelId">The model ID to look up</param>
    /// <returns>List of model provider mappings for the specified model</returns>
    Task<IEnumerable<ModelProviderMapping>> GetMappingsByModelIdAsync(int modelId);

    /// <summary>
    /// Adds a new model provider mapping
    /// </summary>
    /// <param name="mapping">The mapping to add</param>
    /// <returns>True if the operation was successful</returns>
    Task<bool> AddMappingAsync(ModelProviderMapping mapping);

    /// <summary>
    /// Updates an existing model provider mapping
    /// </summary>
    /// <param name="mapping">The mapping to update</param>
    /// <returns>True if the operation was successful</returns>
    Task<bool> UpdateMappingAsync(ModelProviderMapping mapping);

    /// <summary>
    /// Deletes a model provider mapping
    /// </summary>
    /// <param name="id">The ID of the mapping to delete</param>
    /// <returns>True if the operation was successful</returns>
    Task<bool> DeleteMappingAsync(int id);

    /// <summary>
    /// Gets a list of all available providers
    /// </summary>
    /// <returns>List of providers with IDs and names</returns>
    Task<IEnumerable<Provider>> GetProvidersAsync();

    /// <summary>
    /// Resolves associations and conflicts for discovered provider models.
    /// </summary>
    Task<BulkModelMappingPreviewResponse> PreviewBulkMappingsAsync(
        BulkModelMappingPreviewRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves and creates discovered provider model mappings using partial-success semantics.
    /// Equivalent existing mappings are returned as successful idempotent results.
    /// </summary>
    Task<BulkModelMappingCreateResponse> CreateBulkMappingsAsync(
        BulkModelMappingCreateRequest request,
        CancellationToken cancellationToken = default);
}
