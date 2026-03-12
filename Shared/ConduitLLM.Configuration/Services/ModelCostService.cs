using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Configuration.Interfaces;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Services;

/// <summary>
/// Service for managing and retrieving model costs. Pure repository operations — caching is handled by the CachedModelCostService decorator.
/// </summary>
public class ModelCostService : IModelCostService
{
    private readonly IModelCostRepository _modelCostRepository;
    private readonly IModelProviderMappingRepository _modelProviderMappingRepository;
    private readonly ILogger<ModelCostService> _logger;

    /// <summary>
    /// Creates a new instance of the ModelCostService
    /// </summary>
    /// <param name="modelCostRepository">The model cost repository</param>
    /// <param name="modelProviderMappingRepository">The model provider mapping repository</param>
    /// <param name="logger">The logger</param>
    public ModelCostService(
        IModelCostRepository modelCostRepository,
        IModelProviderMappingRepository modelProviderMappingRepository,
        ILogger<ModelCostService> logger)
    {
        _modelCostRepository = modelCostRepository ?? throw new ArgumentNullException(nameof(modelCostRepository));
        _modelProviderMappingRepository = modelProviderMappingRepository ?? throw new ArgumentNullException(nameof(modelProviderMappingRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<ModelCost?> GetCostForModelAsync(string modelId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new ArgumentException("Model ID cannot be empty", nameof(modelId));
        }

        try
        {
            // Get all model costs with their associated ModelProviderTypeAssociations
            var allCosts = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                _modelCostRepository.GetPaginatedAsync, cancellationToken: cancellationToken);

            // Find a cost where one of its associated ModelProviderTypeAssociations has this identifier
            var now = DateTime.UtcNow;
            var modelCost = allCosts
                .Where(cost => cost.IsActive && cost.EffectiveDate <= now)
                .Where(cost => !cost.ExpiryDate.HasValue || cost.ExpiryDate.Value > now)
                .Where(cost => cost.ModelProviderTypeAssociations.Any(assoc =>
                    assoc.Identifier == modelId && assoc.IsEnabled))
                .OrderByDescending(cost => cost.Priority)
                .ThenByDescending(cost => cost.EffectiveDate)
                .FirstOrDefault();

            if (modelCost == null)
            {
                _logger.LogDebug("No model cost found for identifier: {ModelId}", modelId);
            }

            return modelCost;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cost for model {ModelId}", modelId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ModelCost?> GetCostByIdAsync(int modelCostId, CancellationToken cancellationToken = default)
    {
        try
        {
            var modelCost = await _modelCostRepository.GetByIdAsync(modelCostId, cancellationToken);

            if (modelCost == null)
            {
                _logger.LogDebug("No model cost found for ID: {ModelCostId}", modelCostId);
                return null;
            }

            // Validate the cost is active and within date range
            var now = DateTime.UtcNow;
            if (!modelCost.IsActive || modelCost.EffectiveDate > now ||
                (modelCost.ExpiryDate.HasValue && modelCost.ExpiryDate.Value <= now))
            {
                _logger.LogDebug("Model cost ID {ModelCostId} exists but is not active or outside date range", modelCostId);
                return null;
            }

            return modelCost;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cost by ID {ModelCostId}", modelCostId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<ModelCost>> ListModelCostsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                _modelCostRepository.GetPaginatedAsync, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing model costs");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task AddModelCostAsync(ModelCost modelCost, CancellationToken cancellationToken = default)
    {
        if (modelCost == null)
        {
            throw new ArgumentNullException(nameof(modelCost));
        }

        try
        {
            modelCost.CreatedAt = DateTime.UtcNow;
            modelCost.UpdatedAt = DateTime.UtcNow;

            await _modelCostRepository.CreateAsync(modelCost, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding model cost {CostName}", modelCost.CostName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> UpdateModelCostAsync(ModelCost modelCost, CancellationToken cancellationToken = default)
    {
        if (modelCost == null)
        {
            throw new ArgumentNullException(nameof(modelCost));
        }

        try
        {
            var existingCost = await _modelCostRepository.GetByIdAsync(modelCost.Id, cancellationToken);

            if (existingCost == null)
            {
                return false;
            }

            // Update properties
            existingCost.CostName = modelCost.CostName;
            existingCost.InputCostPerMillionTokens = modelCost.InputCostPerMillionTokens;
            existingCost.OutputCostPerMillionTokens = modelCost.OutputCostPerMillionTokens;
            existingCost.EmbeddingCostPerMillionTokens = modelCost.EmbeddingCostPerMillionTokens;
            existingCost.CachedInputCostPerMillionTokens = modelCost.CachedInputCostPerMillionTokens;
            existingCost.CachedInputWriteCostPerMillionTokens = modelCost.CachedInputWriteCostPerMillionTokens;
            existingCost.UpdatedAt = DateTime.UtcNow;

            return await _modelCostRepository.UpdateAsync(existingCost, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating model cost with ID {ModelCostId}", modelCost.Id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteModelCostAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _modelCostRepository.DeleteAsync(id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting model cost with ID {ModelCostId}", id);
            throw;
        }
    }

    /// <inheritdoc />
    public Task ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        // No-op: caching is handled by the CachedModelCostService decorator
        return Task.CompletedTask;
    }
}
