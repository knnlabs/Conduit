using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Routing;
using ConduitLLM.Core.Routing;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Service for managing the LLM router configuration
    /// </summary>
    public class RouterService : ILLMRouterService
    {
        private readonly ILLMRouter _router;
        private readonly IRouterConfigRepository _repository;
        private readonly ILogger<RouterService> _logger;

        /// <summary>
        /// Creates a new RouterService instance
        /// </summary>
        /// <param name="router">The LLM router to manage</param>
        /// <param name="repository">Repository for router configurations</param>
        /// <param name="logger">Logger instance</param>
        public RouterService(
            ILLMRouter router,
            IRouterConfigRepository repository,
            ILogger<RouterService> logger)
        {
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Initializes the router with the latest configuration
        /// </summary>
        public async Task InitializeRouterAsync(CancellationToken cancellationToken = default)
        {
            var config = await _repository.GetRouterConfigAsync(cancellationToken);
            if (config == null)
            {
                _logger.LogInformation("No router configuration found, creating default");
                config = CreateDefaultConfig();
                await _repository.SaveRouterConfigAsync(config, cancellationToken);
            }

            if (_router is DefaultLLMRouter defaultRouter)
            {
                _logger.LogInformation("Initializing router with {ModelCount} model deployments", 
                    config.ModelDeployments?.Count ?? 0);
                defaultRouter.Initialize(config);
            }
            else
            {
                _logger.LogWarning("Router is not a DefaultLLMRouter, cannot initialize with config");
            }
        }

        /// <summary>
        /// Gets the current router configuration
        /// </summary>
        public async Task<RouterConfig> GetRouterConfigAsync(CancellationToken cancellationToken = default)
        {
            var config = await _repository.GetRouterConfigAsync(cancellationToken);
            return config ?? CreateDefaultConfig();
        }

        /// <summary>
        /// Updates the router configuration
        /// </summary>
        public async Task UpdateRouterConfigAsync(RouterConfig config, CancellationToken cancellationToken = default)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            await _repository.SaveRouterConfigAsync(config, cancellationToken);
            
            if (_router is DefaultLLMRouter defaultRouter)
            {
                defaultRouter.Initialize(config);
            }
        }

        /// <summary>
        /// Adds a model deployment to the router
        /// </summary>
        public async Task AddModelDeploymentAsync(ModelDeployment deployment, CancellationToken cancellationToken = default)
        {
            if (deployment == null)
            {
                throw new ArgumentNullException(nameof(deployment));
            }

            var config = await _repository.GetRouterConfigAsync(cancellationToken);
            if (config == null)
            {
                config = CreateDefaultConfig();
            }

            // Remove existing deployment with the same name if present
            config.ModelDeployments.RemoveAll(m => 
                m.DeploymentName.Equals(deployment.DeploymentName, StringComparison.OrdinalIgnoreCase));
            
            // Add the new deployment
            config.ModelDeployments.Add(deployment);
            
            // Save the updated config
            await _repository.SaveRouterConfigAsync(config, cancellationToken);
            
            // Update the router
            if (_router is DefaultLLMRouter defaultRouter)
            {
                defaultRouter.Initialize(config);
            }
        }

        /// <summary>
        /// Updates an existing model deployment
        /// </summary>
        public async Task UpdateModelDeploymentAsync(ModelDeployment deployment, CancellationToken cancellationToken = default)
        {
            await AddModelDeploymentAsync(deployment, cancellationToken);
        }

        /// <summary>
        /// Removes a model deployment from the router
        /// </summary>
        public async Task RemoveModelDeploymentAsync(string deploymentName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(deploymentName))
            {
                throw new ArgumentException("Deployment name cannot be null or empty", nameof(deploymentName));
            }

            var config = await _repository.GetRouterConfigAsync(cancellationToken);
            if (config == null)
            {
                return;
            }

            // Remove the deployment
            int removed = config.ModelDeployments.RemoveAll(m => 
                m.DeploymentName.Equals(deploymentName, StringComparison.OrdinalIgnoreCase));
            
            if (removed > 0)
            {
                // Save the updated config
                await _repository.SaveRouterConfigAsync(config, cancellationToken);
                
                // Update the router
                if (_router is DefaultLLMRouter defaultRouter)
                {
                    defaultRouter.Initialize(config);
                }
            }
        }

        /// <summary>
        /// Gets all available model deployments
        /// </summary>
        public async Task<List<ModelDeployment>> GetModelDeploymentsAsync(CancellationToken cancellationToken = default)
        {
            var config = await _repository.GetRouterConfigAsync(cancellationToken);
            return config?.ModelDeployments ?? new List<ModelDeployment>();
        }

        /// <summary>
        /// Sets fallback models for a primary model
        /// </summary>
        public async Task SetFallbackModelsAsync(string primaryModel, List<string> fallbacks, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(primaryModel))
            {
                throw new ArgumentException("Primary model name cannot be null or empty", nameof(primaryModel));
            }

            var config = await _repository.GetRouterConfigAsync(cancellationToken);
            if (config == null)
            {
                config = CreateDefaultConfig();
            }

            // Update fallbacks mapping
            if (fallbacks == null || fallbacks.Count == 0)
            {
                // Remove the fallback configuration if empty
                if (config.Fallbacks.ContainsKey(primaryModel))
                {
                    config.Fallbacks.Remove(primaryModel);
                }
            }
            else
            {
                // Set the fallbacks
                config.Fallbacks[primaryModel] = fallbacks;
            }
            
            // Save the updated config
            await _repository.SaveRouterConfigAsync(config, cancellationToken);
            
            // Update the router
            if (_router is DefaultLLMRouter defaultRouter)
            {
                if (fallbacks == null || fallbacks.Count == 0)
                {
                    defaultRouter.RemoveFallbacks(primaryModel);
                }
                else
                {
                    defaultRouter.AddFallbackModels(primaryModel, fallbacks);
                }
            }
        }

        /// <summary>
        /// Gets fallback models for a primary model
        /// </summary>
        public async Task<List<string>> GetFallbackModelsAsync(string primaryModel, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(primaryModel))
            {
                throw new ArgumentException("Primary model name cannot be null or empty", nameof(primaryModel));
            }

            var config = await _repository.GetRouterConfigAsync(cancellationToken);
            if (config == null || !config.Fallbacks.TryGetValue(primaryModel, out var fallbacks))
            {
                return new List<string>();
            }

            return fallbacks;
        }

        /// <summary>
        /// Updates the health status of a model deployment
        /// </summary>
        public void UpdateModelHealth(string deploymentName, bool isHealthy)
        {
            if (string.IsNullOrEmpty(deploymentName))
            {
                throw new ArgumentException("Deployment name cannot be null or empty", nameof(deploymentName));
            }

            _router.UpdateModelHealth(deploymentName, isHealthy);
            _logger.LogInformation("Updated model {DeploymentName} health status to {IsHealthy}", 
                deploymentName, isHealthy);
        }

        /// <summary>
        /// Creates a default router configuration
        /// </summary>
        private RouterConfig CreateDefaultConfig()
        {
            return new RouterConfig
            {
                DefaultRoutingStrategy = "simple",
                MaxRetries = 3,
                RetryBaseDelayMs = 500,
                RetryMaxDelayMs = 10000,
                ModelDeployments = new List<ModelDeployment>(),
                Fallbacks = new Dictionary<string, List<string>>()
            };
        }
    }
}
