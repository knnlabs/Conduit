using ConduitLLM.Configuration.DTOs;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Services.Providers
{
    /// <summary>
    /// Implementation of IModelCostService that uses IAdminApiClient to interact with the Admin API.
    /// </summary>
    public class ModelCostServiceProvider : IModelCostService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<ModelCostServiceProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelCostServiceProvider"/> class.
        /// </summary>
        /// <param name="adminApiClient">The Admin API client.</param>
        /// <param name="logger">The logger.</param>
        public ModelCostServiceProvider(
            IAdminApiClient adminApiClient,
            ILogger<ModelCostServiceProvider> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<decimal> CalculateCostAsync(string modelId, int inputTokens, int outputTokens)
        {
            try
            {
                // Try to get the model cost directly from pattern match
                var modelCost = await GetModelCostByPatternAsync(modelId);
                
                if (modelCost == null)
                {
                    // If no matching pattern, try to find a default cost for all models
                    modelCost = await GetModelCostByPatternAsync("default");
                    
                    if (modelCost == null)
                    {
                        _logger.LogWarning("No cost information found for model ID {ModelId} and no default cost available", modelId);
                        return 0m;
                    }
                }

                // Calculate the cost based on input and output tokens
                decimal inputCost = modelCost.InputTokenCost * inputTokens;
                decimal outputCost = modelCost.OutputTokenCost * outputTokens;
                
                return inputCost + outputCost;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating cost for model {ModelId} with {InputTokens} input tokens and {OutputTokens} output tokens", 
                    modelId, inputTokens, outputTokens);
                return 0m;
            }
        }

        /// <inheritdoc />
        public async Task<ModelCostDto?> CreateModelCostAsync(CreateModelCostDto modelCost)
        {
            try
            {
                return await _adminApiClient.CreateModelCostAsync(modelCost);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model cost for model pattern {ModelPattern}", modelCost.ModelIdPattern);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ModelCostDto>> GetAllModelCostsAsync()
        {
            try
            {
                return await _adminApiClient.GetAllModelCostsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all model costs");
                return Enumerable.Empty<ModelCostDto>();
            }
        }

        /// <inheritdoc />
        public async Task<ModelCostDto?> GetModelCostByIdAsync(int id)
        {
            try
            {
                return await _adminApiClient.GetModelCostByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving model cost with ID {ModelCostId}", id);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<ModelCostDto?> GetModelCostByPatternAsync(string modelIdPattern)
        {
            try
            {
                // Admin API doesn't have a direct endpoint for this, so we need to get all costs and filter
                var allCosts = await _adminApiClient.GetAllModelCostsAsync();
                
                // First, try to find an exact match
                var exactMatch = allCosts.FirstOrDefault(c => string.Equals(c.ModelIdPattern, modelIdPattern, StringComparison.OrdinalIgnoreCase));
                if (exactMatch != null)
                {
                    return exactMatch;
                }
                
                // If no exact match, try to find a wildcard match
                // Order by specificity (e.g. "gpt-4-*" is more specific than "*")
                var wildcardMatches = allCosts
                    .Where(c => c.ModelIdPattern.Contains("*"))
                    .OrderByDescending(c => c.ModelIdPattern.Length)
                    .ToList();
                
                foreach (var cost in wildcardMatches)
                {
                    var pattern = cost.ModelIdPattern.Replace("*", ".*");
                    if (System.Text.RegularExpressions.Regex.IsMatch(modelIdPattern, "^" + pattern + "$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        return cost;
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving model cost by pattern {ModelIdPattern}", modelIdPattern);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<ModelCostDto?> UpdateModelCostAsync(int id, UpdateModelCostDto modelCost)
        {
            try
            {
                return await _adminApiClient.UpdateModelCostAsync(id, modelCost);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model cost with ID {ModelCostId}", id);
                return null;
            }
        }
    }
}