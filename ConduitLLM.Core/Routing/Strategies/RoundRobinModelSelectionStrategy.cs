using System.Collections.Generic;
using System.Linq;

using ConduitLLM.Core.Models.Routing;

namespace ConduitLLM.Core.Routing.Strategies
{
    /// <summary>
    /// A round-robin model selection strategy that distributes requests evenly across available models.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This strategy implements a round-robin selection by choosing the model that has been
    /// used the least according to the usage counts. This ensures that requests are distributed
    /// evenly across all available models over time.
    /// </para>
    /// <para>
    /// Round-robin selection is useful for load balancing and for avoiding overloading any
    /// single model deployment, especially in high-traffic scenarios.
    /// </para>
    /// </remarks>
    public class RoundRobinModelSelectionStrategy : IModelSelectionStrategy
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
                .First();
        }
    }
}
