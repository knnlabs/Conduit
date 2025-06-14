using System;
using System.Collections.Generic;
using System.Linq;

using ConduitLLM.Core.Models.Routing;

namespace ConduitLLM.Core.Routing.Strategies
{
    /// <summary>
    /// A model selection strategy that selects the model that has been used the least.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This strategy implements load balancing by selecting the model with the lowest
    /// usage count. It's similar to round-robin but takes the actual usage count into
    /// account rather than just cycling through models in order.
    /// </para>
    /// <para>
    /// Least-used selection is particularly useful when models have different capacities
    /// or when some models may be temporarily unavailable, as it naturally adjusts the
    /// distribution based on actual usage patterns.
    /// </para>
    /// </remarks>
    public class LeastUsedModelSelectionStrategy : IModelSelectionStrategy
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

            // Select the model with the lowest usage count
            return availableModels
                .OrderBy(m => modelUsageCounts.TryGetValue(m, out var count) ? count : 0)
                .FirstOrDefault();
        }
    }
}
