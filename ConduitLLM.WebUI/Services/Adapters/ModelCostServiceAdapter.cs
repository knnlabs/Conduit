using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Services;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services.Adapters
{
    /// <summary>
    /// Adapter that implements both <see cref="ConduitLLM.Configuration.Services.IModelCostService"/>
    /// and <see cref="ConduitLLM.WebUI.Interfaces.IModelCostService"/> using the Admin API client.
    /// </summary>
    public class ModelCostServiceAdapter :
        ConduitLLM.Configuration.Services.IModelCostService,
        ConduitLLM.WebUI.Interfaces.IModelCostService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<ModelCostServiceAdapter> _logger;
        private object _cacheLock = new object();
        private Dictionary<string, ModelCost>? _cache;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelCostServiceAdapter"/> class.
        /// </summary>
        /// <param name="adminApiClient">The Admin API client.</param>
        /// <param name="logger">The logger.</param>
        public ModelCostServiceAdapter(
            IAdminApiClient adminApiClient,
            ILogger<ModelCostServiceAdapter> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<ModelCost?> GetCostForModelAsync(string modelId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_cache != null)
                {
                    // Try to find the model in the cache
                    var bestMatchKey = GetBestMatchingKey(modelId);
                    if (bestMatchKey != null && _cache.TryGetValue(bestMatchKey, out var cachedModelCost))
                    {
                        return cachedModelCost;
                    }
                }

                // If not found in cache, fetch all models and update cache
                var allCosts = await ListModelCostsAsync(cancellationToken);
                
                // Find best matching model cost
                ModelCost? bestMatch = null;
                foreach (var cost in allCosts)
                {
                    if (IsModelMatch(modelId, cost.ModelIdPattern))
                    {
                        if (bestMatch == null || cost.ModelIdPattern.Length > bestMatch.ModelIdPattern.Length)
                        {
                            bestMatch = cost;
                        }
                    }
                }

                return bestMatch;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cost for model {ModelId}", modelId);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<List<ModelCost>> ListModelCostsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Check cache first
                if (_cache != null)
                {
                    return _cache.Values.ToList();
                }

                // If not cached, get from API
                var modelCosts = await _adminApiClient.GetAllModelCostsAsync();
                
                // Convert DTOs to entities
                var entities = modelCosts.Select(dto => new ModelCost
                {
                    Id = dto.Id,
                    ModelIdPattern = dto.ModelIdPattern,
                    InputTokenCost = dto.InputTokenCost,
                    OutputTokenCost = dto.OutputTokenCost,
                    EmbeddingTokenCost = dto.EmbeddingTokenCost,
                    ImageCostPerImage = dto.ImageCostPerImage,
                    CreatedAt = dto.CreatedAt,
                    UpdatedAt = dto.UpdatedAt,
                    // These properties are added for backward compatibility and not in the original DTO
                    Description = "",
                    Priority = 0
                }).ToList();
                
                // Update cache
                UpdateCache(entities);
                
                return entities;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing model costs");
                return new List<ModelCost>();
            }
        }

        /// <inheritdoc />
        public async Task AddModelCostAsync(ModelCost modelCost, CancellationToken cancellationToken = default)
        {
            try
            {
                // Convert entity to DTO
                var dto = new CreateModelCostDto
                {
                    ModelIdPattern = modelCost.ModelIdPattern,
                    InputTokenCost = modelCost.InputTokenCost,
                    OutputTokenCost = modelCost.OutputTokenCost,
                    EmbeddingTokenCost = modelCost.EmbeddingTokenCost,
                    ImageCostPerImage = modelCost.ImageCostPerImage
                    // Description and Priority properties don't exist in the DTO
                };
                
                // Create via API
                var result = await _adminApiClient.CreateModelCostAsync(dto);
                
                // Clear cache to ensure fresh data on next fetch
                ClearCache();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding model cost {ModelIdPattern}", modelCost.ModelIdPattern);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateModelCostAsync(ModelCost modelCost, CancellationToken cancellationToken = default)
        {
            try
            {
                // Convert entity to DTO
                var dto = new UpdateModelCostDto
                {
                    ModelIdPattern = modelCost.ModelIdPattern,
                    InputTokenCost = modelCost.InputTokenCost,
                    OutputTokenCost = modelCost.OutputTokenCost,
                    EmbeddingTokenCost = modelCost.EmbeddingTokenCost,
                    ImageCostPerImage = modelCost.ImageCostPerImage
                    // Description and Priority properties don't exist in the DTO
                };
                
                // Update via API
                var result = await _adminApiClient.UpdateModelCostAsync(modelCost.Id, dto);
                
                // Clear cache to ensure fresh data on next fetch
                ClearCache();
                
                return result != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model cost {Id} {ModelIdPattern}", modelCost.Id, modelCost.ModelIdPattern);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteModelCostAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                // Delete via API
                var result = await _adminApiClient.DeleteModelCostAsync(id);
                
                // Clear cache to ensure fresh data on next fetch
                ClearCache();
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model cost {Id}", id);
                return false;
            }
        }

        /// <inheritdoc />
        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _cache = null;
                _logger.LogDebug("Model cost cache cleared");
            }
        }

        // Helper methods for WebUI adapter interface

        /// <summary>
        /// Gets all model costs.
        /// </summary>
        /// <returns>A collection of model costs.</returns>
        public async Task<IEnumerable<ModelCostDto>> GetAllModelCostsAsync()
        {
            return await _adminApiClient.GetAllModelCostsAsync();
        }

        /// <summary>
        /// Gets a model cost by ID.
        /// </summary>
        /// <param name="id">The ID of the model cost.</param>
        /// <returns>The model cost, or null if not found.</returns>
        public async Task<ModelCostDto?> GetModelCostByIdAsync(int id)
        {
            return await _adminApiClient.GetModelCostByIdAsync(id);
        }

        /// <summary>
        /// Gets a model cost by pattern.
        /// </summary>
        /// <param name="modelIdPattern">The model ID pattern.</param>
        /// <returns>The model cost, or null if not found.</returns>
        public async Task<ModelCostDto?> GetModelCostByPatternAsync(string modelIdPattern)
        {
            try
            {
                var allCosts = await _adminApiClient.GetAllModelCostsAsync();
                return allCosts.FirstOrDefault(c => c.ModelIdPattern == modelIdPattern);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model cost by pattern {ModelIdPattern}", modelIdPattern);
                return null;
            }
        }

        /// <summary>
        /// Creates a new model cost.
        /// </summary>
        /// <param name="modelCost">The model cost to create.</param>
        /// <returns>The created model cost.</returns>
        public async Task<ModelCostDto?> CreateModelCostAsync(CreateModelCostDto modelCost)
        {
            var result = await _adminApiClient.CreateModelCostAsync(modelCost);
            ClearCache();
            return result;
        }

        /// <summary>
        /// Updates a model cost.
        /// </summary>
        /// <param name="id">The ID of the model cost to update.</param>
        /// <param name="modelCost">The updated model cost.</param>
        /// <returns>The updated model cost, or null if the update failed.</returns>
        public async Task<ModelCostDto?> UpdateModelCostAsync(int id, UpdateModelCostDto modelCost)
        {
            var result = await _adminApiClient.UpdateModelCostAsync(id, modelCost);
            ClearCache();
            return result;
        }

        /// <summary>
        /// Calculates the cost for a request.
        /// </summary>
        /// <param name="modelId">The model ID.</param>
        /// <param name="inputTokens">The number of input tokens.</param>
        /// <param name="outputTokens">The number of output tokens.</param>
        /// <returns>The calculated cost.</returns>
        public async Task<decimal> CalculateCostAsync(string modelId, int inputTokens, int outputTokens)
        {
            try
            {
                // Get all model costs
                var allCosts = await _adminApiClient.GetAllModelCostsAsync();
                if (allCosts == null || !allCosts.Any())
                {
                    return 0;
                }

                // Find the best matching model cost pattern
                ModelCostDto? bestMatch = null;
                foreach (var cost in allCosts)
                {
                    if (IsModelMatch(modelId, cost.ModelIdPattern))
                    {
                        if (bestMatch == null || cost.ModelIdPattern.Length > bestMatch.ModelIdPattern.Length)
                        {
                            bestMatch = cost;
                        }
                    }
                }

                // Calculate cost based on the best match
                if (bestMatch != null)
                {
                    return (bestMatch.InputTokenCost * inputTokens / 1000m) + 
                           (bestMatch.OutputTokenCost * outputTokens / 1000m);
                }

                // No matching cost pattern found
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating cost for model {ModelId}", modelId);
                return 0;
            }
        }

        // Private helper methods

        private void UpdateCache(List<ModelCost> modelCosts)
        {
            lock (_cacheLock)
            {
                _cache = modelCosts.ToDictionary(m => m.ModelIdPattern, m => m);
                _logger.LogDebug("Model cost cache updated with {Count} entries", _cache.Count);
            }
        }

        private string? GetBestMatchingKey(string modelId)
        {
            if (_cache == null)
            {
                return null;
            }

            string? bestMatch = null;
            int bestMatchLength = 0;

            foreach (var pattern in _cache.Keys)
            {
                if (IsModelMatch(modelId, pattern) && pattern.Length > bestMatchLength)
                {
                    bestMatch = pattern;
                    bestMatchLength = pattern.Length;
                }
            }

            return bestMatch;
        }

        private bool IsModelMatch(string modelId, string pattern)
        {
            // Handle wildcard pattern at the end (e.g., "gpt-4*")
            if (pattern.EndsWith("*"))
            {
                return modelId.StartsWith(pattern.TrimEnd('*'), StringComparison.OrdinalIgnoreCase);
            }
            
            // Exact match
            return string.Equals(modelId, pattern, StringComparison.OrdinalIgnoreCase);
        }
    }
}