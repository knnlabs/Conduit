using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Routing;
using ConduitLLM.Core.Routing;
using ConduitLLM.WebUI.Data;
using ConduitLLM.WebUI.Interfaces;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Service that manages the LLM router configurations and provides access to the router
    /// </summary>
    public class RouterService : IRouterService
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly ILLMClientFactory _clientFactory;
        private readonly ILogger<RouterService> _logger;
        private readonly IOptionsMonitor<RouterOptions> _routerOptions;
        private readonly IServiceProvider _serviceProvider;
        private ILLMRouter? _router;

        private const string ROUTER_CONFIG_KEY = "RouterConfig";

        /// <summary>
        /// Creates a new instance of the RouterService
        /// </summary>
        public RouterService(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            ILLMClientFactory clientFactory,
            ILogger<RouterService> logger,
            IOptionsMonitor<RouterOptions> routerOptions,
            IServiceProvider serviceProvider)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _routerOptions = routerOptions ?? throw new ArgumentNullException(nameof(routerOptions));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            // Initialize the router (will be done asynchronously)
            Task.Run(InitializeRouterAsync);
        }

        /// <inheritdoc/>
        public ILLMRouter? GetRouter()
        {
            return _router;
        }

        /// <inheritdoc/>
        public async Task<List<ModelDeployment>> GetModelDeploymentsAsync()
        {
            var config = await GetRouterConfigAsync();
            return config.ModelDeployments;
        }

        /// <inheritdoc/>
        public async Task<ModelDeployment?> GetModelDeploymentAsync(string deploymentName)
        {
            var deployments = await GetModelDeploymentsAsync();
            return deployments.FirstOrDefault(d => d.DeploymentName.Equals(deploymentName, StringComparison.OrdinalIgnoreCase));
        }

        /// <inheritdoc/>
        public async Task<bool> SaveModelDeploymentAsync(ModelDeployment deployment)
        {
            try
            {
                var config = await GetRouterConfigAsync();

                // Remove existing deployment with same name if it exists
                config.ModelDeployments.RemoveAll(d => 
                    d.DeploymentName.Equals(deployment.DeploymentName, StringComparison.OrdinalIgnoreCase));

                // Add the new/updated deployment
                config.ModelDeployments.Add(deployment);

                // Save the updated config
                await SaveRouterConfigAsync(config);

                // Update the router if it exists
                if (_router != null)
                {
                    // For DefaultLLMRouter, register the deployment directly
                    _router.UpdateModelHealth(deployment.DeploymentName, deployment.IsHealthy);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving model deployment {DeploymentName}", deployment.DeploymentName);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteModelDeploymentAsync(string deploymentName)
        {
            try
            {
                var config = await GetRouterConfigAsync();
                
                // Check if the deployment exists
                bool deploymentRemoved = config.ModelDeployments.RemoveAll(d => 
                    d.DeploymentName.Equals(deploymentName, StringComparison.OrdinalIgnoreCase)) > 0;
                
                if (!deploymentRemoved)
                {
                    _logger.LogWarning("Deployment {DeploymentName} not found for deletion", deploymentName);
                    return false;
                }
                
                // Update fallbacks that might reference this deployment
                foreach (var key in config.Fallbacks.Keys.ToList())
                {
                    config.Fallbacks[key].RemoveAll(m => 
                        m.Equals(deploymentName, StringComparison.OrdinalIgnoreCase));
                    
                    // If this removal left an empty fallback list, remove the entire entry
                    if (!config.Fallbacks[key].Any())
                    {
                        config.Fallbacks.Remove(key);
                    }
                }
                
                // Save the updated configuration
                await SaveRouterConfigAsync(config);
                
                _logger.LogInformation("Deleted deployment {DeploymentName}", deploymentName);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model deployment {DeploymentName}", deploymentName);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, List<string>>> GetFallbackConfigurationsAsync()
        {
            var config = await GetRouterConfigAsync();
            return config.Fallbacks;
        }

        /// <inheritdoc/>
        public async Task<bool> SetFallbackConfigurationAsync(string primaryModel, List<string> fallbackModels)
        {
            try
            {
                var config = await GetRouterConfigAsync();
                
                // Update the fallback configuration
                config.Fallbacks[primaryModel] = fallbackModels;
                
                // Save the updated configuration
                await SaveRouterConfigAsync(config);
                
                // Update the router if it exists
                if (_router != null && _router is DefaultLLMRouter defaultRouter)
                {
                    // Add the fallbacks to the router
                    defaultRouter.AddFallbackModels(primaryModel, fallbackModels);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting fallback configuration for model {PrimaryModel}", primaryModel);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> RemoveFallbackConfigurationAsync(string primaryModel)
        {
            try
            {
                var config = await GetRouterConfigAsync();
                
                // Check if the configuration exists
                if (!config.Fallbacks.ContainsKey(primaryModel))
                {
                    _logger.LogWarning("No fallback configuration found for model {PrimaryModel}", primaryModel);
                    return false;
                }
                
                // Remove the fallback configuration
                config.Fallbacks.Remove(primaryModel);
                
                // Save the updated configuration
                await SaveRouterConfigAsync(config);
                
                // Update the router if it exists
                if (_router != null && _router is DefaultLLMRouter defaultRouter)
                {
                    // Remove the fallbacks from the router
                    defaultRouter.RemoveFallbacks(primaryModel);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing fallback configuration for model {PrimaryModel}", primaryModel);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<RouterConfig> GetRouterConfigAsync()
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            
            // Try to load the configuration from the database
            var dbSetting = await dbContext.GlobalSettings
                .FirstOrDefaultAsync(s => s.Key == ROUTER_CONFIG_KEY);
            
            if (dbSetting != null)
            {
                try
                {
                    // Deserialize the configuration
                    var config = System.Text.Json.JsonSerializer.Deserialize<RouterConfig>(dbSetting.Value);
                    if (config != null)
                    {
                        return config;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deserializing router configuration");
                }
            }
            
            // If we get here, either the configuration doesn't exist or couldn't be deserialized
            // Create a new configuration from options or defaults
            var options = _routerOptions.CurrentValue;
            if (options != null)
            {
                return CreateConfigFromOptions(options);
            }
            
            // Return a default configuration
            return new RouterConfig();
        }

        /// <inheritdoc/>
        public async Task<RouterStatus> GetRouterStatusAsync()
        {
            try
            {
                var config = await GetRouterConfigAsync();
                var isEnabled = _router != null;

                return new RouterStatus
                {
                    Config = config,
                    IsEnabled = isEnabled
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

        /// <inheritdoc/>
        public async Task<bool> UpdateRouterConfigAsync(RouterConfig config)
        {
            try
            {
                // Save the configuration
                await SaveRouterConfigAsync(config);
                
                // Reinitialize the router with the new configuration
                await InitializeRouterAsync();
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating router configuration");
                return false;
            }
        }

        /// <summary>
        /// Initializes the router from configuration
        /// </summary>
        public async Task InitializeRouterAsync()
        {
            try
            {
                // Get the router configuration
                var config = await GetRouterConfigAsync();
                
                // Check if routing is enabled
                var options = _routerOptions.CurrentValue;
                if (options == null || !options.Enabled)
                {
                    _logger.LogInformation("Router is disabled in configuration");
                    _router = null;
                    return;
                }
                
                // Create a new router
                var routerLogger = (ILogger<DefaultLLMRouter>?)_serviceProvider.GetService(typeof(ILogger<DefaultLLMRouter>)) 
                    ?? _logger as ILogger<DefaultLLMRouter>;
                    
                if (routerLogger == null)
                {
                    _logger.LogWarning("Could not create a properly typed logger for the router. Using null logger.");
                    routerLogger = NullLogger<DefaultLLMRouter>.Instance;
                }
                
                _router = new DefaultLLMRouter(_clientFactory, routerLogger);
                
                // Configure the router
                if (_router is DefaultLLMRouter defaultRouter)
                {
                    // Register model deployments
                    foreach (var deployment in config.ModelDeployments)
                    {
                        // Register the model with the router
                        defaultRouter.UpdateModelHealth(deployment.DeploymentName, deployment.IsHealthy);
                    }
                    
                    // Register fallbacks
                    foreach (var fallback in config.Fallbacks)
                    {
                        defaultRouter.AddFallbackModels(fallback.Key, fallback.Value);
                    }
                }
                
                _logger.LogInformation("Router initialized with {DeploymentCount} deployments", 
                    config.ModelDeployments.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing router");
                _router = null;
            }
        }

        /// <summary>
        /// Saves the router configuration to the database
        /// </summary>
        private async Task SaveRouterConfigAsync(RouterConfig config)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            // Serialize the configuration
            string serializedConfig = System.Text.Json.JsonSerializer.Serialize(config);

            // Check if we already have a setting
            var existingSetting = await dbContext.GlobalSettings
                .FirstOrDefaultAsync(s => s.Key == ROUTER_CONFIG_KEY);

            if (existingSetting != null)
            {
                // Update the existing setting
                existingSetting.Value = serializedConfig;
            }
            else
            {
                // Create a new setting
                dbContext.GlobalSettings.Add(new GlobalSetting
                {
                    Key = ROUTER_CONFIG_KEY,
                    Value = serializedConfig
                });
            }

            // Save changes
            await dbContext.SaveChangesAsync();
        }

        /// <summary>
        /// Creates a router configuration from options
        /// </summary>
        private RouterConfig CreateConfigFromOptions(RouterOptions options)
        {
            var config = new RouterConfig
            {
                DefaultRoutingStrategy = options.DefaultRoutingStrategy,
                MaxRetries = options.MaxRetries,
                RetryBaseDelayMs = options.RetryBaseDelayMs,
                RetryMaxDelayMs = options.RetryMaxDelayMs
            };

            // Map model deployments from options
            foreach (var deployment in options.ModelDeployments)
            {
                config.ModelDeployments.Add(new ModelDeployment
                {
                    DeploymentName = deployment.DeploymentName,
                    ModelAlias = deployment.ModelAlias,
                    RPM = deployment.RPM,
                    TPM = deployment.TPM,
                    InputTokenCostPer1K = deployment.InputTokenCostPer1K,
                    OutputTokenCostPer1K = deployment.OutputTokenCostPer1K,
                    Priority = deployment.Priority,
                    IsHealthy = true
                });
            }

            // Parse fallback rules from options
            foreach (var rule in options.FallbackRules)
            {
                // Expected format: "primary_model:fallback_model1,fallback_model2"
                var parts = rule.Split(':', 2);
                if (parts.Length == 2)
                {
                    string primaryModel = parts[0].Trim();
                    string[] fallbackModels = parts[1].Split(',')
                        .Select(m => m.Trim())
                        .Where(m => !string.IsNullOrEmpty(m))
                        .ToArray();

                    if (!string.IsNullOrEmpty(primaryModel) && fallbackModels.Length > 0)
                    {
                        config.Fallbacks[primaryModel] = fallbackModels.ToList();
                    }
                }
            }

            return config;
        }
    }
}
