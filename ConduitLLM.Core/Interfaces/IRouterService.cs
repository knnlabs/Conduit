using ConduitLLM.Core.Models.Routing;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for a service that manages LLM router configuration
    /// </summary>
    public interface ILLMRouterService
    {
        /// <summary>
        /// Initializes the router with the latest configuration
        /// </summary>
        Task InitializeRouterAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current router configuration
        /// </summary>
        Task<RouterConfig> GetRouterConfigAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the router configuration
        /// </summary>
        Task UpdateRouterConfigAsync(RouterConfig config, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a model deployment to the router
        /// </summary>
        Task AddModelDeploymentAsync(ModelDeployment deployment, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing model deployment
        /// </summary>
        Task UpdateModelDeploymentAsync(ModelDeployment deployment, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a model deployment from the router
        /// </summary>
        Task RemoveModelDeploymentAsync(string deploymentName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all available model deployments
        /// </summary>
        Task<List<ModelDeployment>> GetModelDeploymentsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets fallback models for a primary model
        /// </summary>
        Task SetFallbackModelsAsync(string primaryModel, List<string> fallbacks, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets fallback models for a primary model
        /// </summary>
        Task<List<string>> GetFallbackModelsAsync(string primaryModel, CancellationToken cancellationToken = default);

    }
}
