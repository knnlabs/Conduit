using ConduitLLM.Functions.Entities;

namespace ConduitLLM.Functions.Interfaces;

/// <summary>
/// Repository interface for managing function cost mappings
/// </summary>
public interface IFunctionCostMappingRepository
{
    /// <summary>
    /// Gets a cost mapping by ID
    /// </summary>
    /// <param name="id">The mapping ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The cost mapping or null if not found</returns>
    Task<FunctionCostMapping?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all cost mappings for a function configuration
    /// </summary>
    /// <param name="functionConfigurationId">The function configuration ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of cost mappings</returns>
    Task<List<FunctionCostMapping>> GetByFunctionConfigurationIdAsync(int functionConfigurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active cost mapping for a function configuration
    /// </summary>
    /// <param name="functionConfigurationId">The function configuration ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The active cost mapping or null if not found</returns>
    Task<FunctionCostMapping?> GetActiveMappingAsync(int functionConfigurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new cost mapping
    /// </summary>
    /// <param name="mapping">The mapping to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The ID of the created mapping</returns>
    Task<int> CreateAsync(FunctionCostMapping mapping, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing cost mapping
    /// </summary>
    /// <param name="mapping">The mapping to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateAsync(FunctionCostMapping mapping, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a cost mapping by ID
    /// </summary>
    /// <param name="id">The mapping ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates all cost mappings for a function configuration
    /// </summary>
    /// <param name="functionConfigurationId">The function configuration ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeactivateAllForFunctionAsync(int functionConfigurationId, CancellationToken cancellationToken = default);
}
