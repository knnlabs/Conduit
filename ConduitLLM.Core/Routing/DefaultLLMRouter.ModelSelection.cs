using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Models.Routing;
using ConduitLLM.Core.Routing.Strategies;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Routing
{
    /// <summary>
    /// Model selection and routing strategy functionality for the DefaultLLMRouter.
    /// </summary>
    public partial class DefaultLLMRouter
    {
        /// <summary>
        /// Selects the most appropriate model based on the specified strategy and current system state.
        /// </summary>
        /// <param name="requestedModel">The model name originally requested by the client, or null if no specific model was requested.</param>
        /// <param name="strategy">The routing strategy to use for selection (e.g., "simple", "roundrobin", "leastcost").</param>
        /// <param name="excludeModels">List of model names to exclude from consideration (typically models that have already been attempted).</param>
        /// <param name="cancellationToken">A token for cancelling the operation.</param>
        /// <param name="visionRequest">Set to true if the request contains images and requires a vision-capable model.</param>
        /// <returns>The name of the selected model, or null if no suitable model could be found.</returns>
        /// <remarks>
        /// This method implements the core model selection logic:
        /// 
        /// 1. Builds a candidate list based on the requested model and available fallbacks
        /// 2. Filters out excluded models and unhealthy models
        /// 3. Gets the appropriate strategy from the factory
        /// 4. Delegates model selection to the strategy implementation
        /// 
        /// If no specific model was requested, it will consider all available models.
        /// If the strategy is not recognized, it defaults to the "simple" strategy.
        /// </remarks>
        private async Task<string?> SelectModelAsync(
            string? requestedModel,
            string strategy,
            List<string> excludeModels,
            CancellationToken cancellationToken,
            bool visionRequest = false)
        {
            // Small delay to make this actually async
            await Task.Delay(1, cancellationToken);

            // Get filtered list of available models
            var (availableModels, availableDeployments) = await GetFilteredAvailableModelsAsync(
                requestedModel, excludeModels, cancellationToken);

            if (!availableModels.Any())
            {
                _logger.LogWarning("No available models found for requestedModel={RequestedModel}", requestedModel);
                return null;
            }

            // Handle passthrough strategy as a special case
            if (IsPassthroughStrategy(strategy))
            {
                // Even in passthrough mode, we need to check vision capability if required
                if (visionRequest && _capabilityDetector != null)
                {
                    var firstModel = availableModels.FirstOrDefault();
                    if (firstModel != null)
                    {
                        string modelAlias = GetModelAliasForDeployment(firstModel);
                        if (!_capabilityDetector.HasVisionCapability(modelAlias))
                        {
                            _logger.LogWarning("Requested model {Model} does not support vision capabilities required by this request",
                                modelAlias);
                            return null;
                        }
                    }
                }
                return availableModels.FirstOrDefault();
            }

            // Select model using the appropriate strategy, filtering for vision-capable models if needed
            return SelectModelUsingStrategy(strategy, availableModels, availableDeployments, visionRequest);
        }

        /// <summary>
        /// Gets a filtered list of available models based on requested model and exclusions.
        /// </summary>
        private async Task<(List<string> AvailableModels, Dictionary<string, ModelDeployment> AvailableDeployments)>
            GetFilteredAvailableModelsAsync(string? requestedModel, List<string> excludeModels, CancellationToken cancellationToken)
        {
            // Add small delay to ensure method is truly async
            await Task.Delay(1, cancellationToken);

            // Build candidate models list
            var candidateModels = BuildCandidateModelsList(requestedModel, excludeModels);

            // Filter to only healthy models
            var availableModels = FilterHealthyModels(candidateModels);

            // Get deployment information for available models
            var availableDeployments = GetAvailableDeployments(availableModels);

            return (availableModels, availableDeployments);
        }

        /// <summary>
        /// Determines if the strategy is a passthrough strategy.
        /// </summary>
        private bool IsPassthroughStrategy(string strategy)
        {
            return strategy.Equals("passthrough", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Selects a model using the appropriate strategy implementation.
        /// </summary>
        private string? SelectModelUsingStrategy(
            string strategy,
            List<string> availableModels,
            Dictionary<string, ModelDeployment> availableDeployments,
            bool visionRequired = false)
        {
            // If vision is required, filter models to only vision-capable ones
            var candidateModels = availableModels;
            if (visionRequired && _capabilityDetector != null)
            {
                candidateModels = availableModels
                    .Where(model => _capabilityDetector.HasVisionCapability(GetModelAliasForDeployment(model)))
                    .ToList();

                if (!candidateModels.Any())
                {
                    _logger.LogWarning("No vision-capable models available from the {Count} candidate models",
                        availableModels.Count);
                    return null;
                }

                _logger.LogInformation("Found {Count} vision-capable models out of {TotalCount} candidates",
                    candidateModels.Count, availableModels.Count);
            }

            // Use the strategy factory to get the appropriate strategy and delegate selection
            var modelSelectionStrategy = ModelSelectionStrategyFactory.GetStrategy(strategy);

            _logger.LogDebug("Using {Strategy} strategy to select from {ModelCount} models",
                strategy, candidateModels.Count);

            return modelSelectionStrategy.SelectModel(
                candidateModels,
                availableDeployments.Where(kv => candidateModels.Contains(kv.Key))
                                   .ToDictionary(kv => kv.Key, kv => kv.Value),
                _modelUsageCount);
        }

        /// <summary>
        /// Builds a list of candidate models based on the requested model and available fallbacks.
        /// </summary>
        /// <param name="requestedModel">The model name originally requested by the client.</param>
        /// <param name="excludeModels">List of model names to exclude from consideration.</param>
        /// <returns>A list of candidate model names.</returns>
        private List<string> BuildCandidateModelsList(string? requestedModel, List<string> excludeModels)
        {
            List<string> candidateModels = new();

            // If we have a specific requested model and it's not in the excluded list, start with that
            if (!string.IsNullOrEmpty(requestedModel) && !excludeModels.Contains(requestedModel))
            {
                // Find any deployments that correspond to this model alias
                var matchingDeployments = _modelDeployments.Values
                    .Where(d => d.ModelAlias.Equals(requestedModel, StringComparison.OrdinalIgnoreCase))
                    .Select(d => d.DeploymentName)
                    .ToList();

                if (matchingDeployments.Any())
                {
                    candidateModels.AddRange(matchingDeployments);
                }
                else
                {
                    // No matching deployments, treat as deployment name directly
                    candidateModels.Add(requestedModel);
                }

                // Add fallbacks for this model if available
                if (_fallbackModels.TryGetValue(requestedModel, out var fallbacks))
                {
                    candidateModels.AddRange(fallbacks.Where(m => !excludeModels.Contains(m)));
                }
            }

            // If no candidates yet, use all available models
            if (!candidateModels.Any())
            {
                candidateModels = _modelDeployments.Keys
                    .Where(m => !excludeModels.Contains(m))
                    .ToList();
            }

            return candidateModels;
        }

        /// <summary>
        /// Filters a list of candidate models (currently no filtering applied).
        /// </summary>
        /// <param name="candidateModels">The list of candidate model names.</param>
        /// <returns>The same list of candidate models.</returns>
        private List<string> FilterHealthyModels(List<string> candidateModels)
        {
            // Health filtering has been removed - all models are considered available
            return candidateModels;
        }

        /// <summary>
        /// Converts a list of model names to a dictionary of their deployment information.
        /// </summary>
        /// <param name="modelNames">The list of model names to convert.</param>
        /// <returns>A dictionary mapping model names to their deployment information.</returns>
        private Dictionary<string, ModelDeployment> GetAvailableDeployments(List<string> modelNames)
        {
            return modelNames
                .Where(m => _modelDeployments.ContainsKey(m))
                .ToDictionary(
                    m => m,
                    m => _modelDeployments[m],
                    StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines the routing strategy to use based on input and defaults.
        /// </summary>
        /// <param name="requestedStrategy">The strategy requested, or null to use default.</param>
        /// <returns>The strategy name to use for routing.</returns>
        private string DetermineRoutingStrategy(string? requestedStrategy)
        {
            return requestedStrategy ?? _defaultRoutingStrategy;
        }

        /// <summary>
        /// Gets the model alias to use with the client for a deployment
        /// </summary>
        private string GetModelAliasForDeployment(string deploymentName)
        {
            if (_modelDeployments.TryGetValue(deploymentName, out var deployment))
            {
                return deployment.ModelAlias;
            }
            return deploymentName; // Fallback to the deployment name if not found
        }
    }
}