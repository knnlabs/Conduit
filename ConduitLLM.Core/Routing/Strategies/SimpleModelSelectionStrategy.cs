using System.Collections.Generic;

using ConduitLLM.Core.Models.Routing;

namespace ConduitLLM.Core.Routing.Strategies
{
    /// <summary>
    /// A simple model selection strategy that selects the first available model.
    /// </summary>
    /// <remarks>
    /// This is the most basic strategy and serves as a fallback when no specific
    /// optimization is needed. It simply returns the first model in the list of
    /// available models.
    /// </remarks>
    public class SimpleModelSelectionStrategy : IModelSelectionStrategy
    {
        /// <inheritdoc/>
        public string? SelectModel(
            IReadOnlyList<string> availableModels,
            IReadOnlyDictionary<string, ModelDeployment> modelDeployments,
            IReadOnlyDictionary<string, int> modelUsageCounts)
        {
            return availableModels.Count > 0 ? availableModels[0] : null;
        }
    }
}
