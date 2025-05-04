using System;
using System.Collections.Generic;
using System.Linq;
using ConduitLLM.Core.Models.Routing;

namespace ConduitLLM.Core.Routing.Strategies
{
    /// <summary>
    /// A model selection strategy that selects the model with the lowest observed latency.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This strategy optimizes for performance by selecting the model with the lowest
    /// average response latency. The latency information is gathered from previous requests
    /// and is continuously updated as new requests are processed.
    /// </para>
    /// <para>
    /// This strategy is ideal for latency-sensitive applications where response time
    /// is more important than other factors like cost.
    /// </para>
    /// </remarks>
    public class LeastLatencyModelSelectionStrategy : IModelSelectionStrategy
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

            // Select the model with the lowest average latency
            return availableDeployments
                .OrderBy(d => d!.AverageLatencyMs)
                .Select(d => d!.DeploymentName)
                .FirstOrDefault();
        }
    }
}