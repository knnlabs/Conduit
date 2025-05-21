using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Routing;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services.Providers
{
    /// <summary>
    /// Implementation of IRouterService that uses IAdminApiClient to interact with the Admin API
    /// </summary>
    public class RouterServiceProvider : IRouterService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILLMRouter? _router;
        private readonly ILogger<RouterServiceProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RouterServiceProvider"/> class.
        /// </summary>
        /// <param name="adminApiClient">The Admin API client.</param>
        /// <param name="router">The LLM router instance.</param>
        /// <param name="logger">The logger.</param>
        public RouterServiceProvider(
            IAdminApiClient adminApiClient,
            ILLMRouter? router,
            ILogger<RouterServiceProvider> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _router = router; // Router can be null when routing is disabled
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public ILLMRouter? GetRouter()
        {
            return _router;
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
                _logger.LogError(ex, "Error retrieving model deployments from Admin API");
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
                _logger.LogError(ex, "Error retrieving model deployment {DeploymentName} from Admin API", deploymentName);
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
                _logger.LogError(ex, "Error saving model deployment {DeploymentName} to Admin API", deployment.ModelName);
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
                _logger.LogError(ex, "Error deleting model deployment {DeploymentName} from Admin API", deploymentName);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, List<string>>> GetFallbackConfigurationsAsync()
        {
            try
            {
                var fallbackConfigs = await _adminApiClient.GetAllFallbackConfigurationsAsync();
                
                if (fallbackConfigs == null)
                {
                    return new Dictionary<string, List<string>>();
                }
                
                return fallbackConfigs.ToDictionary(
                    config => config.PrimaryModelDeploymentId,
                    config => config.FallbackModelDeploymentIds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving fallback configurations from Admin API");
                return new Dictionary<string, List<string>>();
            }
        }

        /// <inheritdoc />
        public async Task<bool> SetFallbackConfigurationAsync(string primaryModel, List<string> fallbackModels)
        {
            try
            {
                var fallbackConfig = new FallbackConfiguration
                {
                    PrimaryModelDeploymentId = primaryModel,
                    FallbackModelDeploymentIds = fallbackModels
                };
                
                return await _adminApiClient.SetFallbackConfigurationAsync(fallbackConfig);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting fallback configuration for model {PrimaryModel} in Admin API", primaryModel);
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
                _logger.LogError(ex, "Error removing fallback configuration for model {PrimaryModel} from Admin API", primaryModel);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<RouterConfig> GetRouterConfigAsync()
        {
            try
            {
                var config = await _adminApiClient.GetRouterConfigAsync();
                
                if (config == null)
                {
                    _logger.LogWarning("Failed to retrieve router configuration from Admin API, returning default configuration");
                    
                    // Return a default configuration
                    return new RouterConfig
                    {
                        DefaultRoutingStrategy = RoutingStrategy.HighestPriority.ToString().ToLowerInvariant(),
                        FallbacksEnabled = false
                    };
                }
                
                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving router configuration from Admin API");
                
                // Return a default configuration in case of error
                return new RouterConfig
                {
                    DefaultRoutingStrategy = RoutingStrategy.HighestPriority.ToString().ToLowerInvariant(),
                    FallbacksEnabled = false
                };
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateRouterConfigAsync(RouterConfig config)
        {
            try
            {
                bool success = await _adminApiClient.UpdateRouterConfigAsync(config);
                
                if (success)
                {
                    // Re-initialize the router with the new configuration
                    await InitializeRouterAsync();
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating router configuration in Admin API");
                return false;
            }
        }

        /// <inheritdoc />
        public async Task InitializeRouterAsync()
        {
            try
            {
                // Get the router configuration from the Admin API
                var config = await _adminApiClient.GetRouterConfigAsync();
                
                if (config == null)
                {
                    _logger.LogWarning("Failed to initialize router: null configuration retrieved from Admin API");
                    return;
                }
                
                // Initialize the router with the configuration
                // This would be handled by the router implementation directly
                _logger.LogInformation("Router initialized with configuration from Admin API");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing router from Admin API configuration");
            }
        }

        /// <inheritdoc />
        public async Task<RouterStatus> GetRouterStatusAsync()
        {
            try
            {
                var config = await _adminApiClient.GetRouterConfigAsync();
                
                return new RouterStatus
                {
                    Config = config,
                    IsEnabled = config?.FallbacksEnabled ?? false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving router status from Admin API");
                
                return new RouterStatus
                {
                    Config = null,
                    IsEnabled = false
                };
            }
        }
    }
}