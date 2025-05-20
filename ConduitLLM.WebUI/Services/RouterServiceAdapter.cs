using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Routing;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Options;
using Microsoft.Extensions.Options;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Adapter service for router operations that can use either direct repository access or the Admin API
/// </summary>
public class RouterServiceAdapter : IRouterService
{
    private readonly RouterService _repositoryService;
    private readonly IAdminApiClient _adminApiClient;
    private readonly AdminApiOptions _adminApiOptions;
    private readonly ILogger<RouterServiceAdapter> _logger;
    
    /// <summary>
    /// Initializes a new instance of the RouterServiceAdapter class
    /// </summary>
    /// <param name="repositoryService">The repository-based router service</param>
    /// <param name="adminApiClient">The Admin API client</param>
    /// <param name="adminApiOptions">The Admin API options</param>
    /// <param name="logger">The logger</param>
    public RouterServiceAdapter(
        RouterService repositoryService,
        IAdminApiClient adminApiClient,
        IOptions<AdminApiOptions> adminApiOptions,
        ILogger<RouterServiceAdapter> logger)
    {
        _repositoryService = repositoryService ?? throw new ArgumentNullException(nameof(repositoryService));
        _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
        _adminApiOptions = adminApiOptions?.Value ?? throw new ArgumentNullException(nameof(adminApiOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <inheritdoc />
    public ILLMRouter? GetRouter()
    {
        // Always use the repository service for this method as it provides direct access to the router instance
        return _repositoryService.GetRouter();
    }
    
    /// <inheritdoc />
    public async Task<List<ModelDeployment>> GetModelDeploymentsAsync()
    {
        if (_adminApiOptions.Enabled)
        {
            try
            {
                return await _adminApiClient.GetAllModelDeploymentsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model deployments from Admin API, falling back to repository");
                return await _repositoryService.GetModelDeploymentsAsync();
            }
        }
        
        return await _repositoryService.GetModelDeploymentsAsync();
    }
    
    /// <inheritdoc />
    public async Task<ModelDeployment?> GetModelDeploymentAsync(string deploymentName)
    {
        if (_adminApiOptions.Enabled)
        {
            try
            {
                return await _adminApiClient.GetModelDeploymentAsync(deploymentName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model deployment {DeploymentName} from Admin API, falling back to repository", deploymentName);
                return await _repositoryService.GetModelDeploymentAsync(deploymentName);
            }
        }
        
        return await _repositoryService.GetModelDeploymentAsync(deploymentName);
    }
    
    /// <inheritdoc />
    public async Task<bool> SaveModelDeploymentAsync(ModelDeployment deployment)
    {
        if (_adminApiOptions.Enabled)
        {
            try
            {
                var success = await _adminApiClient.SaveModelDeploymentAsync(deployment);
                
                if (!success)
                {
                    _logger.LogWarning("Failed to save model deployment through Admin API, falling back to repository");
                    return await _repositoryService.SaveModelDeploymentAsync(deployment);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving model deployment through Admin API, falling back to repository");
                return await _repositoryService.SaveModelDeploymentAsync(deployment);
            }
        }
        
        return await _repositoryService.SaveModelDeploymentAsync(deployment);
    }
    
    /// <inheritdoc />
    public async Task<bool> DeleteModelDeploymentAsync(string deploymentName)
    {
        if (_adminApiOptions.Enabled)
        {
            try
            {
                var success = await _adminApiClient.DeleteModelDeploymentAsync(deploymentName);
                
                if (!success)
                {
                    _logger.LogWarning("Failed to delete model deployment through Admin API, falling back to repository");
                    return await _repositoryService.DeleteModelDeploymentAsync(deploymentName);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model deployment through Admin API, falling back to repository");
                return await _repositoryService.DeleteModelDeploymentAsync(deploymentName);
            }
        }
        
        return await _repositoryService.DeleteModelDeploymentAsync(deploymentName);
    }
    
    /// <inheritdoc />
    public async Task<Dictionary<string, List<string>>> GetFallbackConfigurationsAsync()
    {
        if (_adminApiOptions.Enabled)
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
                _logger.LogError(ex, "Error getting fallback configurations from Admin API, falling back to repository");
                return await _repositoryService.GetFallbackConfigurationsAsync();
            }
        }

        return await _repositoryService.GetFallbackConfigurationsAsync();
    }
    
    /// <inheritdoc />
    public async Task<bool> SetFallbackConfigurationAsync(string primaryModel, List<string> fallbackModels)
    {
        if (_adminApiOptions.Enabled)
        {
            try
            {
                // Create a FallbackConfiguration object to pass to the API
                var fallbackConfig = new FallbackConfiguration
                {
                    PrimaryModelDeploymentId = primaryModel,
                    FallbackModelDeploymentIds = fallbackModels
                };

                var success = await _adminApiClient.SetFallbackConfigurationAsync(fallbackConfig);

                if (!success)
                {
                    _logger.LogWarning("Failed to set fallback configuration through Admin API, falling back to repository");
                    return await _repositoryService.SetFallbackConfigurationAsync(primaryModel, fallbackModels);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting fallback configuration through Admin API, falling back to repository");
                return await _repositoryService.SetFallbackConfigurationAsync(primaryModel, fallbackModels);
            }
        }

        return await _repositoryService.SetFallbackConfigurationAsync(primaryModel, fallbackModels);
    }
    
    /// <inheritdoc />
    public async Task<bool> RemoveFallbackConfigurationAsync(string primaryModel)
    {
        if (_adminApiOptions.Enabled)
        {
            try
            {
                var success = await _adminApiClient.RemoveFallbackConfigurationAsync(primaryModel);
                
                if (!success)
                {
                    _logger.LogWarning("Failed to remove fallback configuration through Admin API, falling back to repository");
                    return await _repositoryService.RemoveFallbackConfigurationAsync(primaryModel);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing fallback configuration through Admin API, falling back to repository");
                return await _repositoryService.RemoveFallbackConfigurationAsync(primaryModel);
            }
        }
        
        return await _repositoryService.RemoveFallbackConfigurationAsync(primaryModel);
    }
    
    /// <inheritdoc />
    public async Task<RouterConfig> GetRouterConfigAsync()
    {
        if (_adminApiOptions.Enabled)
        {
            try
            {
                var result = await _adminApiClient.GetRouterConfigAsync();
                return result ?? await _repositoryService.GetRouterConfigAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting router configuration from Admin API, falling back to repository");
                return await _repositoryService.GetRouterConfigAsync();
            }
        }
        
        return await _repositoryService.GetRouterConfigAsync();
    }
    
    /// <inheritdoc />
    public async Task<bool> UpdateRouterConfigAsync(RouterConfig config)
    {
        if (_adminApiOptions.Enabled)
        {
            try
            {
                var success = await _adminApiClient.UpdateRouterConfigAsync(config);
                
                if (!success)
                {
                    _logger.LogWarning("Failed to update router configuration through Admin API, falling back to repository");
                    return await _repositoryService.UpdateRouterConfigAsync(config);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating router configuration through Admin API, falling back to repository");
                return await _repositoryService.UpdateRouterConfigAsync(config);
            }
        }
        
        return await _repositoryService.UpdateRouterConfigAsync(config);
    }
    
    /// <inheritdoc />
    public async Task InitializeRouterAsync()
    {
        // Always use the repository service for this method as it initializes the router instance
        await _repositoryService.InitializeRouterAsync();
    }
    
    /// <inheritdoc />
    public async Task<RouterStatus> GetRouterStatusAsync()
    {
        // Always use the repository service for this method as it provides access to the router instance
        return await _repositoryService.GetRouterStatusAsync();
    }
}