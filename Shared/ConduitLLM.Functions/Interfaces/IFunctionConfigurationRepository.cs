using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Enums;

namespace ConduitLLM.Functions.Interfaces;

/// <summary>
/// Repository interface for managing function configurations
/// </summary>
public interface IFunctionConfigurationRepository
{
    /// <summary>
    /// Gets a function configuration by ID
    /// </summary>
    /// <param name="id">The function configuration ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The function configuration or null if not found</returns>
    Task<FunctionConfiguration?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets multiple function configurations by their IDs
    /// </summary>
    /// <param name="ids">The function configuration IDs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of function configurations matching the provided IDs</returns>
    Task<List<FunctionConfiguration>> GetByIdsAsync(List<int> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a function configuration by name
    /// </summary>
    /// <param name="configurationName">The configuration name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The function configuration or null if not found</returns>
    Task<FunctionConfiguration?> GetByNameAsync(string configurationName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all function configurations
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of all function configurations</returns>
    /// <remarks>
    /// DEPRECATED: Use GetAllUnboundedAsync() for unbounded queries,
    /// or GetPaginatedAsync() for bounded pagination.
    /// </remarks>
    [Obsolete("Use GetAllUnboundedAsync() for cache warming/exports, or GetPaginatedAsync() for bounded queries. This method will be removed in a future version.")]
    Task<List<FunctionConfiguration>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all function configurations WITHOUT pagination. Use ONLY for legitimate batch operations
    /// like cache warming, exports, or migrations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of all function configurations</returns>
    Task<List<FunctionConfiguration>> GetAllUnboundedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paginated list of function configurations.
    /// </summary>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A tuple containing the items and total count</returns>
    Task<(List<FunctionConfiguration> Items, int TotalCount)> GetPaginatedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all enabled function configurations
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of enabled function configurations</returns>
    Task<List<FunctionConfiguration>> GetAllEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all function configurations by provider type
    /// </summary>
    /// <param name="providerType">The provider type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of function configurations for the specified provider type</returns>
    Task<List<FunctionConfiguration>> GetByProviderTypeAsync(FunctionProviderType providerType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all function configurations by purpose
    /// </summary>
    /// <param name="purpose">The function purpose</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of function configurations for the specified purpose</returns>
    Task<List<FunctionConfiguration>> GetByPurposeAsync(FunctionPurpose purpose, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new function configuration
    /// </summary>
    /// <param name="functionConfiguration">The function configuration to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The ID of the created function configuration</returns>
    Task<int> CreateAsync(FunctionConfiguration functionConfiguration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing function configuration
    /// </summary>
    /// <param name="functionConfiguration">The function configuration to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the entity was updated</returns>
    Task<bool> UpdateAsync(FunctionConfiguration functionConfiguration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a function configuration by ID
    /// </summary>
    /// <param name="id">The function configuration ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the entity was deleted</returns>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a function configuration name already exists
    /// </summary>
    /// <param name="configurationName">The configuration name to check</param>
    /// <param name="excludeId">Optional ID to exclude from the check (for updates)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the name exists, false otherwise</returns>
    Task<bool> NameExistsAsync(string configurationName, int? excludeId = null, CancellationToken cancellationToken = default);
}
