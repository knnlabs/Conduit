using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces;

/// <summary>
/// Repository interface for managing global settings.
/// Inherits standard CRUD operations from IRepositoryBase.
/// </summary>
public interface IGlobalSettingRepository : IRepositoryBase<GlobalSetting, int>
{
    /// <summary>
    /// Gets a global setting by key.
    /// </summary>
    /// <param name="key">The setting key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The global setting entity or null if not found</returns>
    Task<GlobalSetting?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all global settings.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of all global settings</returns>
    /// <remarks>
    /// DEPRECATED: Use GetAllUnboundedAsync() from IRepositoryBase for unbounded queries,
    /// or GetPaginatedAsync() for bounded pagination.
    /// </remarks>
    [Obsolete("Use GetAllUnboundedAsync() for cache warming/exports, or GetPaginatedAsync() for bounded queries. This method will be removed in a future version.")]
    Task<List<GlobalSetting>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates or creates a global setting.
    /// </summary>
    /// <param name="key">The setting key</param>
    /// <param name="value">The setting value</param>
    /// <param name="description">Optional description</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the operation was successful, false otherwise</returns>
    Task<bool> UpsertAsync(string key, string value, string? description = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a global setting by key.
    /// </summary>
    /// <param name="key">The key of the global setting to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the deletion was successful, false otherwise</returns>
    Task<bool> DeleteByKeyAsync(string key, CancellationToken cancellationToken = default);
}
