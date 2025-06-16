using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Routing;

namespace ConduitLLM.WebUI.Interfaces
{
    /// <summary>
    /// Interface for the router service that manages LLM router configurations
    /// </summary>
    public interface IRouterService
    {
        /// <summary>
        /// Gets the current router instance
        /// </summary>
        /// <returns>The router instance or null if not configured</returns>
        ILLMRouter? GetRouter();

        /// <summary>
        /// Gets all model deployments configured in the router
        /// </summary>
        /// <returns>List of model deployments</returns>
        Task<List<ModelDeployment>> GetModelDeploymentsAsync();

        /// <summary>
        /// Gets a specific model deployment by name
        /// </summary>
        /// <param name="deploymentName">The name of the deployment to retrieve</param>
        /// <returns>The model deployment or null if not found</returns>
        Task<ModelDeployment?> GetModelDeploymentAsync(string deploymentName);

        /// <summary>
        /// Creates or updates a model deployment
        /// </summary>
        /// <param name="deployment">The deployment to create or update</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> SaveModelDeploymentAsync(ModelDeployment deployment);

        /// <summary>
        /// Deletes a model deployment
        /// </summary>
        /// <param name="deploymentName">The name of the deployment to delete</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> DeleteModelDeploymentAsync(string deploymentName);

        /// <summary>
        /// Gets all fallback configurations
        /// </summary>
        /// <returns>Dictionary of fallback configurations</returns>
        Task<Dictionary<string, List<string>>> GetFallbackConfigurationsAsync();

        /// <summary>
        /// Sets a fallback configuration
        /// </summary>
        /// <param name="primaryModel">The primary model name</param>
        /// <param name="fallbackModels">List of fallback model names</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> SetFallbackConfigurationAsync(string primaryModel, List<string> fallbackModels);

        /// <summary>
        /// Removes a fallback configuration
        /// </summary>
        /// <param name="primaryModel">The primary model name to remove fallbacks for</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> RemoveFallbackConfigurationAsync(string primaryModel);

        /// <summary>
        /// Gets the current router configuration
        /// </summary>
        /// <returns>The router configuration</returns>
        Task<RouterConfig> GetRouterConfigAsync();

        /// <summary>
        /// Updates the router configuration
        /// </summary>
        /// <param name="config">The new router configuration</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> UpdateRouterConfigAsync(RouterConfig config);

        /// <summary>
        /// Initializes the router from configuration
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        Task InitializeRouterAsync();

        /// <summary>
        /// Gets the current router status including configuration and enabled state
        /// </summary>
        /// <returns>A RouterStatus object containing the configuration and enabled state</returns>
        Task<RouterStatus> GetRouterStatusAsync();
    }

    /// <summary>
    /// Represents the status of the router including its configuration and whether it's enabled
    /// </summary>
    public class RouterStatus
    {
        /// <summary>
        /// The current router configuration
        /// </summary>
        public RouterConfig? Config { get; set; }

        /// <summary>
        /// Whether the router is currently enabled
        /// </summary>
        public bool IsEnabled { get; set; }
    }
}
