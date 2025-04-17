using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Services;

/// <summary>
/// Service for managing model costs across different LLM models
/// </summary>
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
    /// Clears the cache for model costs
    /// </summary>
    void ClearCache();
}
