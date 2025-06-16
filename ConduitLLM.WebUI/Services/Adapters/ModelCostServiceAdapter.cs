using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs;
using ConduitLLM.WebUI.Interfaces;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services.Adapters
{
    /// <summary>
    /// Adapter that bridges the model cost service interface with the Admin API client
    /// </summary>
    public class ModelCostServiceAdapter
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<ModelCostServiceAdapter> _logger;

        public ModelCostServiceAdapter(IAdminApiClient adminApiClient, ILogger<ModelCostServiceAdapter> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets all model costs
        /// </summary>
        /// <returns>Collection of model costs</returns>
        public async Task<IEnumerable<ModelCostDto>> GetAllModelCostsAsync()
        {
            return await _adminApiClient.GetAllModelCostsAsync();
        }

        /// <summary>
        /// Gets a model cost by ID
        /// </summary>
        /// <param name="id">The model cost ID</param>
        /// <returns>The model cost or null if not found</returns>
        public async Task<ModelCostDto?> GetModelCostByIdAsync(int id)
        {
            return await _adminApiClient.GetModelCostByIdAsync(id);
        }

        /// <summary>
        /// Gets a model cost by pattern match
        /// </summary>
        /// <param name="pattern">The model pattern to search for</param>
        /// <returns>The first matching model cost or null if not found</returns>
        public async Task<ModelCostDto?> GetModelCostByPatternAsync(string pattern)
        {
            try
            {
                var allCosts = await _adminApiClient.GetAllModelCostsAsync();
                if (allCosts == null) return null;

                // Find the first cost entry where the pattern matches
                // The pattern might contain wildcards, so we need to handle that
                var normalizedPattern = pattern.Replace("*", "").ToLowerInvariant();
                
                return allCosts.FirstOrDefault(c => 
                {
                    if (c.ModelIdPattern == null) return false;
                    
                    var normalizedModelPattern = c.ModelIdPattern.Replace("*", "").ToLowerInvariant();
                    
                    // Check if the patterns match (ignoring wildcards)
                    if (normalizedModelPattern == normalizedPattern)
                        return true;
                    
                    // Check if one pattern is a prefix of the other
                    if (normalizedModelPattern.StartsWith(normalizedPattern) || 
                        normalizedPattern.StartsWith(normalizedModelPattern))
                        return true;
                    
                    return false;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model cost by pattern {Pattern}", pattern);
                return null;
            }
        }

        /// <summary>
        /// Creates a new model cost
        /// </summary>
        /// <param name="modelCost">The model cost to create</param>
        /// <returns>The created model cost</returns>
        public async Task<ModelCostDto?> CreateModelCostAsync(CreateModelCostDto modelCost)
        {
            return await _adminApiClient.CreateModelCostAsync(modelCost);
        }

        /// <summary>
        /// Updates a model cost
        /// </summary>
        /// <param name="id">The model cost ID</param>
        /// <param name="modelCost">The updated model cost data</param>
        /// <returns>The updated model cost</returns>
        public async Task<ModelCostDto?> UpdateModelCostAsync(int id, UpdateModelCostDto modelCost)
        {
            return await _adminApiClient.UpdateModelCostAsync(id, modelCost);
        }

        /// <summary>
        /// Deletes a model cost
        /// </summary>
        /// <param name="id">The model cost ID</param>
        /// <returns>True if deleted successfully</returns>
        public async Task<bool> DeleteModelCostAsync(int id)
        {
            return await _adminApiClient.DeleteModelCostAsync(id);
        }

        /// <summary>
        /// Calculates the cost for a specific model and token usage
        /// </summary>
        /// <param name="modelName">The name of the model</param>
        /// <param name="inputTokens">Number of input tokens</param>
        /// <param name="outputTokens">Number of output tokens</param>
        /// <returns>The calculated cost</returns>
        public async Task<decimal> CalculateCostAsync(string modelName, int inputTokens, int outputTokens)
        {
            try
            {
                var allCosts = await _adminApiClient.GetAllModelCostsAsync();
                if (allCosts == null) return 0m;

                // Find the best matching cost entry for the model
                ModelCostDto? matchingCost = null;
                
                foreach (var cost in allCosts)
                {
                    if (cost.ModelIdPattern == null) continue;
                    
                    // Check if the model name matches the pattern
                    var pattern = cost.ModelIdPattern.Replace("*", "");
                    
                    if (modelName.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        // Prefer more specific matches
                        if (matchingCost == null || cost.ModelIdPattern.Length > matchingCost.ModelIdPattern.Length)
                        {
                            matchingCost = cost;
                        }
                    }
                }

                if (matchingCost == null)
                {
                    _logger.LogWarning("No cost configuration found for model {ModelName}", modelName);
                    return 0m;
                }

                // Calculate the cost
                // Costs are typically stored per 1K tokens
                var inputCost = (matchingCost.InputTokenCost * inputTokens) / 1000m;
                var outputCost = (matchingCost.OutputTokenCost * outputTokens) / 1000m;
                
                return inputCost + outputCost;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating cost for model {ModelName}", modelName);
                return 0m;
            }
        }
    }
}