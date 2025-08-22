using ConduitLLM.Core.Models.Routing;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for model selection strategies used by the router.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This interface defines the contract for model selection strategies that can be used
    /// by the LLM router to select the most appropriate model for a request based on different criteria.
    /// </para>
    /// <para>
    /// Implementation of this interface should encapsulate a specific selection algorithm,
    /// such as least cost, round robin, or least latency. The router can then dynamically
    /// switch between strategies based on configuration or request requirements.
    /// </para>
    /// </remarks>
    public interface IModelSelectionStrategy
    {
        /// <summary>
        /// Selects the most appropriate model from a list of available models.
        /// </summary>
        /// <param name="availableModels">The list of available model names.</param>
        /// <param name="modelDeployments">Dictionary of model deployments keyed by deployment name.</param>
        /// <param name="modelUsageCounts">Dictionary of model usage counts keyed by model name.</param>
        /// <returns>The name of the selected model, or null if no model could be selected.</returns>
        /// <remarks>
        /// Implementations should select a model based on the strategy's specific algorithm
        /// (e.g., least cost, least used, random) using the provided model information.
        /// </remarks>
        string? SelectModel(
            IReadOnlyList<string> availableModels,
            IReadOnlyDictionary<string, ModelDeployment> modelDeployments,
            IReadOnlyDictionary<string, int> modelUsageCounts);
    }
}
