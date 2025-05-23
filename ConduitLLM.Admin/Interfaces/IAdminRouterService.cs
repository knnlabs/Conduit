using ConduitLLM.Core.Models.Routing;

namespace ConduitLLM.Admin.Interfaces;

/// <summary>
/// Service interface for managing routing configuration through the Admin API
/// </summary>
public interface IAdminRouterService
{
    /// <summary>
    /// Gets the current router configuration
    /// </summary>
    /// <returns>The router configuration</returns>
    Task<RouterConfig> GetRouterConfigAsync();
    
    /// <summary>
    /// Updates the router configuration
    /// </summary>
    /// <param name="config">The new router configuration</param>
    /// <returns>True if the update was successful</returns>
    Task<bool> UpdateRouterConfigAsync(RouterConfig config);
    
    /// <summary>
    /// Gets all model deployments
    /// </summary>
    /// <returns>List of all model deployments</returns>
    Task<List<ModelDeployment>> GetModelDeploymentsAsync();
    
    /// <summary>
    /// Gets a specific model deployment
    /// </summary>
    /// <param name="deploymentName">The name of the deployment</param>
    /// <returns>The model deployment, or null if not found</returns>
    Task<ModelDeployment?> GetModelDeploymentAsync(string deploymentName);
    
    /// <summary>
    /// Saves a model deployment (creates or updates)
    /// </summary>
    /// <param name="deployment">The deployment to save</param>
    /// <returns>True if the operation was successful</returns>
    Task<bool> SaveModelDeploymentAsync(ModelDeployment deployment);
    
    /// <summary>
    /// Deletes a model deployment
    /// </summary>
    /// <param name="deploymentName">The name of the deployment to delete</param>
    /// <returns>True if the deletion was successful</returns>
    Task<bool> DeleteModelDeploymentAsync(string deploymentName);
    
    /// <summary>
    /// Gets all fallback configurations
    /// </summary>
    /// <returns>Dictionary mapping primary models to their fallback models</returns>
    Task<Dictionary<string, List<string>>> GetFallbackConfigurationsAsync();
    
    /// <summary>
    /// Sets a fallback configuration
    /// </summary>
    /// <param name="primaryModel">The primary model</param>
    /// <param name="fallbackModels">The fallback models</param>
    /// <returns>True if the operation was successful</returns>
    Task<bool> SetFallbackConfigurationAsync(string primaryModel, List<string> fallbackModels);
    
    /// <summary>
    /// Removes a fallback configuration
    /// </summary>
    /// <param name="primaryModel">The primary model</param>
    /// <returns>True if the removal was successful</returns>
    Task<bool> RemoveFallbackConfigurationAsync(string primaryModel);
}