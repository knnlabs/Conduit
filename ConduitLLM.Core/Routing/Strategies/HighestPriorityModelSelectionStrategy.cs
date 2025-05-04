using System;
using System.Collections.Generic;
using System.Linq;
using ConduitLLM.Core.Models.Routing;

namespace ConduitLLM.Core.Routing.Strategies
{
    /// <summary>
    /// A model selection strategy that selects models based on a pre-defined priority order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This strategy selects models based on their assigned priority values, with lower
    /// priority values indicating higher priority (i.e., priority 1 is higher than priority 2).
    /// </para>
    /// <para>
    /// This approach allows administrators to explicitly control the order in which models
    /// are selected, regardless of other factors like cost or latency. It's useful when
    /// certain models are preferred over others for reasons not captured by other metrics.
    /// </para>
    /// </remarks>
    public class HighestPriorityModelSelectionStrategy : IModelSelectionStrategy
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

            // Select the model with the highest priority (lowest priority number)
            return availableDeployments
                .OrderBy(d => d!.Priority)
                .Select(d => d!.DeploymentName)
                .FirstOrDefault();
        }
    }
}