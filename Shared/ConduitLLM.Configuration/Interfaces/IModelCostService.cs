using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces;

/// <summary>
/// Service for managing model costs across different LLM models.
/// </summary>
/// <remarks>
/// This interface is part of a three-layer architecture where each layer has its own IModelCostService:
/// 1. Configuration layer (this interface) - Handles database operations and caching
/// 2. Core layer - Provides a simplified interface for cost calculations
/// 3. WebAdmin layer - Provides admin API operations
/// 
/// This separation follows Clean Architecture principles and maintains proper layer boundaries.
/// </remarks>
public interface IModelCostService
{
    /// <summary>
    /// Gets the cost for a specific model identifier
    /// </summary>
    /// <param name="modelId">The model identifier to get costs for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The model cost or null if not found</returns>
    Task<ModelCost?> GetCostForModelAsync(string modelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a model cost by its ID. This is the preferred lookup method when the ID is known.
    /// </summary>
    /// <param name="modelCostId">The ID of the model cost to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The model cost or null if not found</returns>
    Task<ModelCost?> GetCostByIdAsync(int modelCostId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all model costs in the system
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all model costs</returns>
    Task<List<ModelCost>> ListModelCostsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new model cost to the system
    /// </summary>
    /// <param name="modelCost">The model cost to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AddModelCostAsync(ModelCost modelCost, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing model cost
    /// </summary>
    /// <param name="modelCost">The model cost to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successfully updated, false if not found</returns>
    Task<bool> UpdateModelCostAsync(ModelCost modelCost, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a model cost by its ID
    /// </summary>
    /// <param name="id">The ID of the model cost to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successfully deleted, false if not found</returns>
    Task<bool> DeleteModelCostAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the cache for model costs synchronously.
    /// </summary>
    /// <remarks>
    /// This method uses blocking async patterns internally which can cause thread pool starvation.
    /// Prefer using <see cref="ClearCacheAsync"/> instead.
    /// </remarks>
    [Obsolete("Use ClearCacheAsync instead. This synchronous method may cause thread pool starvation.")]
    void ClearCache();

    /// <summary>
    /// Clears the cache for model costs asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task ClearCacheAsync(CancellationToken cancellationToken = default);
}
