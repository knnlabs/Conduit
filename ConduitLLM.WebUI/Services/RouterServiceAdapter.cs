using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Routing;
using ConduitLLM.WebUI.Extensions;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Options;
using Microsoft.Extensions.Options;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Adapter service for router operations using the Admin API
/// </summary>
public class RouterServiceAdapter : IRouterService
{
    private readonly IAdminApiClient _adminApiClient;
    private readonly ILogger<RouterServiceAdapter> _logger;
    
    // Flag to indicate if initialization has been performed
    private bool _routerInitialized = false;
    
    /// <summary>
    /// Initializes a new instance of the RouterServiceAdapter class
    /// </summary>
    /// <param name="adminApiClient">The Admin API client</param>
    /// <param name="logger">The logger</param>
    public RouterServiceAdapter(
        IAdminApiClient adminApiClient,
        ILogger<RouterServiceAdapter> logger)
    {
        _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <inheritdoc />
    public ILLMRouter? GetRouter()
    {
        // We need to ensure the router is initialized before returning it
        if (!_routerInitialized)
        {
            _logger.LogWarning("Router not initialized. Call InitializeRouterAsync first.");
            return null;
        }
        
        // In the adapter pattern implementation, we don't maintain a local router instance
        // Instead, all operations are delegated to the Admin API
        _logger.LogInformation("GetRouter() is not directly supported in the adapter pattern implementation");
        return null;
    }
    
    /// <inheritdoc />
    public async Task<List<ModelDeployment>> GetModelDeploymentsAsync()
    {
        try
        {
            var deployments = await _adminApiClient.GetAllModelDeploymentsAsync();
            return deployments ?? new List<ModelDeployment>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting model deployments from Admin API");
            return new List<ModelDeployment>();
        }
    }
    
    /// <inheritdoc />
    public async Task<ModelDeployment?> GetModelDeploymentAsync(string deploymentName)
    {
        try
        {
            return await _adminApiClient.GetModelDeploymentAsync(deploymentName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting model deployment {DeploymentName} from Admin API", deploymentName);
            return null;
        }
    }
    
    /// <inheritdoc />
    public async Task<bool> SaveModelDeploymentAsync(ModelDeployment deployment)
    {
        try
        {
            return await _adminApiClient.SaveModelDeploymentAsync(deployment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving model deployment through Admin API");
            return false;
        }
    }
    
    /// <inheritdoc />
    public async Task<bool> DeleteModelDeploymentAsync(string deploymentName)
    {
        try
        {
            return await _adminApiClient.DeleteModelDeploymentAsync(deploymentName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting model deployment through Admin API");
            return false;
        }
    }
    
    /// <inheritdoc />
    public async Task<Dictionary<string, List<string>>> GetFallbackConfigurationsAsync()
    {
        try
        {
            var fallbackConfigs = await _adminApiClient.GetAllFallbackConfigurationsAsync();
            // Convert from FallbackConfiguration objects to Dictionary<string, List<string>>
            var result = new Dictionary<string, List<string>>();
            foreach (var config in fallbackConfigs)
            {
                result[config.PrimaryModelDeploymentId] = config.FallbackModelDeploymentIds?.ToList() ?? new List<string>();
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting fallback configurations from Admin API");
            return new Dictionary<string, List<string>>();
        }
    }
    
    /// <inheritdoc />
    public async Task<bool> SetFallbackConfigurationAsync(string primaryModel, List<string> fallbackModels)
    {
        try
        {
            // Create a FallbackConfiguration object to pass to the API
            var fallbackConfig = new FallbackConfiguration
            {
                PrimaryModelDeploymentId = primaryModel,
                FallbackModelDeploymentIds = fallbackModels
            };

            return await _adminApiClient.SetFallbackConfigurationAsync(fallbackConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting fallback configuration through Admin API");
            return false;
        }
    }
    
    /// <inheritdoc />
    public async Task<bool> RemoveFallbackConfigurationAsync(string primaryModel)
    {
        try
        {
            return await _adminApiClient.RemoveFallbackConfigurationAsync(primaryModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing fallback configuration through Admin API");
            return false;
        }
    }
    
    /// <inheritdoc />
    public async Task<RouterConfig> GetRouterConfigAsync()
    {
        try
        {
            var result = await _adminApiClient.GetRouterConfigAsync();
            if (result == null)
            {
                _logger.LogWarning("Router configuration not found or not initialized yet in Admin API");
                // Return a default router configuration
                return new RouterConfig
                {
                    DefaultRoutingStrategy = "Simple",
                    FallbacksEnabled = false
                };
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting router configuration from Admin API");
            // Return a default router configuration
            return new RouterConfig
            {
                DefaultRoutingStrategy = "Simple",
                FallbacksEnabled = false
            };
        }
    }
    
    /// <inheritdoc />
    public async Task<bool> UpdateRouterConfigAsync(RouterConfig config)
    {
        try
        {
            // Update Admin API configuration 
            var success = await _adminApiClient.UpdateRouterConfigAsync(config);
            
            if (success && _routerInitialized)
            {
                // If successful and we're initialized, reinitialize to update local state
                try
                {
                    await InitializeRouterAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating initialization state after Admin API update");
                }
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating router configuration through Admin API");
            return false;
        }
    }
    
    /// <inheritdoc />
    public async Task InitializeRouterAsync()
    {
        try
        {
            // Get the router configuration
            var config = await GetRouterConfigAsync();
            
            if (config != null)
            {
                // Get all model deployments
                var deployments = await GetModelDeploymentsAsync();
                
                // Get fallback configurations
                var fallbackConfigs = await GetFallbackConfigurationsAsync();
                
                // Initialize a local router instance if needed for direct access
                // This would require creating a method to build a router from the configurations
                // For now, we'll just set a flag to indicate the router is initialized
                _routerInitialized = true;
                _logger.LogInformation("Router initialized with {DeploymentCount} deployments and {FallbackCount} fallback configurations", 
                    deployments.Count, fallbackConfigs.Count);
            }
            else
            {
                _logger.LogWarning("Failed to initialize router - configuration not available");
                _routerInitialized = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing router");
            _routerInitialized = false;
        }
    }
    
    /// <inheritdoc />
    public async Task<RouterStatus> GetRouterStatusAsync()
    {
        try
        {
            var config = await GetRouterConfigAsync();
            
            return new RouterStatus
            {
                Config = config,
                IsEnabled = config?.Enabled() ?? false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting router status");
            return new RouterStatus
            {
                Config = null,
                IsEnabled = false
            };
        }
    }
}