using System;
using System.Collections.Generic;
using System.Linq;

using ConduitLLM.Core.Models.Routing;

using ConduitLLM.Core.Interfaces;
namespace ConduitLLM.Core.Routing.Strategies
{
    /// <summary>
    /// A model selection strategy that selects the model with the lowest token cost.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This strategy optimizes for cost by selecting the model with the lowest token cost.
    /// It considers both input and output token costs, with input costs being the primary
    /// sorting criterion and output costs being the secondary criterion.
    /// </para>
    /// <para>
    /// This strategy is ideal for cost-sensitive applications where minimizing expenses
    /// is more important than other factors like performance or features.
    /// </para>
    /// </remarks>
    public class LeastCostModelSelectionStrategy : IModelSelectionStrategy
    {
        /// <inheritdoc/>
        public string? SelectModel(
            IReadOnlyList<string> availableModels,
            IReadOnlyDictionary<string, ModelDeployment> modelDeployments,
            IReadOnlyDictionary<string, int> modelUsageCounts)
        {
            if (availableModels.Count == 0)
            {
                return null;
            }

            // Extract deployments for the available models
            var availableDeployments = availableModels
                .Select(m => modelDeployments.TryGetValue(m, out var deployment) ? deployment : null)
                .Where(d => d != null)
                .ToList();

            if (availableDeployments.Count == 0)
            {
                // Fall back to simple strategy if no deployment info is available
                return availableModels[0];
            }

            // Select the model with the lowest token costs
            return availableDeployments
                .OrderBy(d => d!.InputTokenCostPer1K ?? decimal.MaxValue)
                .ThenBy(d => d!.OutputTokenCostPer1K ?? decimal.MaxValue)
                .Select(d => d!.DeploymentName)
                .FirstOrDefault();
        }
    }
}
