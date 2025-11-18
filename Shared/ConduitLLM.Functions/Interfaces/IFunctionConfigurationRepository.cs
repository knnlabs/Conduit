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
    Task<List<FunctionConfiguration>> GetAllAsync(CancellationToken cancellationToken = default);

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
    Task UpdateAsync(FunctionConfiguration functionConfiguration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a function configuration by ID
    /// </summary>
    /// <param name="id">The function configuration ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a function configuration name already exists
    /// </summary>
    /// <param name="configurationName">The configuration name to check</param>
    /// <param name="excludeId">Optional ID to exclude from the check (for updates)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the name exists, false otherwise</returns>
    Task<bool> NameExistsAsync(string configurationName, int? excludeId = null, CancellationToken cancellationToken = default);
}
