using ConduitLLM.Core.Interfaces;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ConduitLLM.Configuration.Options;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Background service that periodically checks the health of model deployments
    /// </summary>
    public class ModelHealthCheckService : BackgroundService
    {
        private readonly ILogger<ModelHealthCheckService> _logger;
        private readonly IOptionsMonitor<RouterOptions> _routerOptions;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _defaultCheckInterval = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Creates a new instance of ModelHealthCheckService
        /// </summary>
        public ModelHealthCheckService(
            ILogger<ModelHealthCheckService> logger,
            IOptionsMonitor<RouterOptions> routerOptions,
            IServiceProvider serviceProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _routerOptions = routerOptions ?? throw new ArgumentNullException(nameof(routerOptions));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <inheritdoc/>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Model health check service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Only perform health checks if router is enabled
                    if (_routerOptions.CurrentValue.Enabled)
                    {
                        await PerformHealthChecksAsync(stoppingToken);
                    }

                    // Wait for the next check interval
                    await Task.Delay(_defaultCheckInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Normal shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error performing model health checks");
                    
                    // Wait a bit before retrying after an error
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }

            _logger.LogInformation("Model health check service is stopping.");
        }

        /// <summary>
        /// Performs health checks on all model deployments that have health checking enabled
        /// </summary>
        private async Task PerformHealthChecksAsync(CancellationToken stoppingToken)
        {
            // Create a scope to resolve scoped services
            using var scope = _serviceProvider.CreateScope();
            var routerService = scope.ServiceProvider.GetRequiredService<IRouterService>();
            var clientFactory = scope.ServiceProvider.GetRequiredService<ILLMClientFactory>();

            try
            {
                // Get all model deployments from the router configuration
                var deployments = await routerService.GetModelDeploymentsAsync();
                
                // Filter to only those that have health checking enabled
                var deploymentsToCheck = deployments
                    .Where(d => d.HealthCheckEnabled && d.IsEnabled)
                    .ToList();
                
                if (deploymentsToCheck.Count == 0)
                {
                    _logger.LogDebug("No model deployments with health checking enabled");
                    return;
                }
                
                _logger.LogInformation("Checking health of {Count} model deployments", deploymentsToCheck.Count);

                // Check each deployment
                foreach (var deployment in deploymentsToCheck)
                {
                    try
                    {
                        _logger.LogDebug(
                            "Checking health of model deployment {ModelName} ({ProviderName})",
                            deployment.ModelName,
                            deployment.ProviderName);
                        
                        // Get the LLM client for this provider
                        var client = clientFactory.GetClientByProvider(deployment.ProviderName);
                        
                        if (client != null)
                        {
                            bool isHealthy = false;
                            
                            try
                            {
                                // Check if the model is listed by the provider
                                var models = await client.ListModelsAsync(
                                    cancellationToken: stoppingToken);
                                
                                // Consider the model healthy if it's in the list or if the list is empty 
                                // (some providers don't implement ListModelsAsync)
                                isHealthy = models == null || models.Count == 0 || models.Contains(deployment.ModelName);
                                
                                if (!isHealthy)
                                {
                                    _logger.LogWarning(
                                        "Model {ModelName} not found in available models from provider {ProviderName}",
                                        deployment.ModelName,
                                        deployment.ProviderName);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(
                                    ex,
                                    "Error listing models from provider {ProviderName} for health check",
                                    deployment.ProviderName);
                                isHealthy = false;
                            }
                            
                            // Update the model health status if it has changed
                            if (deployment.IsHealthy != isHealthy)
                            {
                                _logger.LogInformation(
                                    "Model deployment {ModelName} ({ProviderName}) health status changed to {IsHealthy}",
                                    deployment.ModelName,
                                    deployment.ProviderName,
                                    isHealthy);
                                
                                // Update the health status in the router
                                var router = routerService.GetRouter();
                                if (router != null)
                                {
                                    router.UpdateModelHealth(deployment.ModelName, isHealthy);
                                }
                                
                                // Update the deployment in the database
                                deployment.IsHealthy = isHealthy;
                                await routerService.SaveModelDeploymentAsync(deployment);
                            }
                        }
                        else
                        {
                            // If we can't create a client, the model is unhealthy
                            if (deployment.IsHealthy)
                            {
                                _logger.LogWarning(
                                    "Unable to create client for model deployment {ModelName} ({ProviderName}), marking as unhealthy",
                                    deployment.ModelName,
                                    deployment.ProviderName);
                                
                                // Update the health status in the router
                                var router = routerService.GetRouter();
                                if (router != null)
                                {
                                    router.UpdateModelHealth(deployment.ModelName, false);
                                }
                                
                                // Update the deployment in the database
                                deployment.IsHealthy = false;
                                await routerService.SaveModelDeploymentAsync(deployment);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Error checking health of model deployment {ModelName} ({ProviderName})",
                            deployment.ModelName,
                            deployment.ProviderName);
                        
                        // Mark the model as unhealthy on error
                        if (deployment.IsHealthy)
                        {
                            // Update the health status in the router
                            var router = routerService.GetRouter();
                            if (router != null)
                            {
                                router.UpdateModelHealth(deployment.ModelName, false);
                            }
                            
                            // Update the deployment in the database
                            deployment.IsHealthy = false;
                            await routerService.SaveModelDeploymentAsync(deployment);
                        }
                    }
                    
                    // Add a short delay between checks to avoid rate limiting
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing model health checks");
            }
        }
    }
}
