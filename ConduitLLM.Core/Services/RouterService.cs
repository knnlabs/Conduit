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
    /// Service for managing the LLM router configuration and model deployments.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The RouterService provides a unified interface for managing the routing configuration
    /// that determines how requests are directed to different LLM providers and models. It
    /// handles operations such as:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Initializing the router with the latest configuration</description></item>
    ///   <item><description>Managing model deployments (adding, updating, removing)</description></item>
    ///   <item><description>Configuring fallback models for high availability</description></item>
    ///   <item><description>Updating model health status</description></item>
    /// </list>
    /// <para>
    /// This service acts as a bridge between the persistence layer (router configuration repository)
    /// and the runtime routing logic implemented by <see cref="ILLMRouter"/>.
    /// </para>
    /// </remarks>
    public class RouterService : ILLMRouterService
    {
        private readonly ILLMRouter _router;
        private readonly IRouterConfigRepository _repository;
        private readonly ILogger<RouterService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RouterService"/> class.
        /// </summary>
        /// <param name="router">The LLM router implementation that handles runtime routing decisions.</param>
        /// <param name="repository">Repository for persisting and retrieving router configurations.</param>
        /// <param name="logger">Logger for recording diagnostic information.</param>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
        /// <remarks>
        /// The service requires three components to function properly:
        /// <list type="bullet">
        ///   <item>
        ///     <description>
        ///       An <see cref="ILLMRouter"/> implementation that performs the actual routing logic
        ///     </description>
        ///   </item>
        ///   <item>
        ///     <description>
        ///       An <see cref="IRouterConfigRepository"/> for configuration persistence
        ///     </description>
        ///   </item>
        ///   <item>
        ///     <description>
        ///       A logger component for diagnostic information
        ///     </description>
        ///   </item>
        /// </list>
        /// </remarks>
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
        /// Initializes the router with the latest configuration from the repository.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation if needed.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// <para>
        /// This method performs the following steps:
        /// </para>
        /// <list type="number">
        ///   <item>
        ///     <description>Retrieves the current router configuration from the repository</description>
        ///   </item>
        ///   <item>
        ///     <description>If no configuration exists, creates a default configuration and saves it</description>
        ///   </item>
        ///   <item>
        ///     <description>Initializes the router with the configuration</description>
        ///   </item>
        /// </list>
        /// <para>
        /// This method should be called during application startup to ensure the router
        /// has the correct configuration loaded.
        /// </para>
        /// <para>
        /// Note: This method requires the router to be of type <see cref="DefaultLLMRouter"/>
        /// to initialize it with the configuration. If the router is of a different type,
        /// a warning is logged and no initialization is performed.
        /// </para>
        /// </remarks>
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
        /// Gets the current router configuration from the repository.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation if needed.</param>
        /// <returns>
        /// The current router configuration, or a default configuration if none exists.
        /// </returns>
        /// <remarks>
        /// This method never returns null. If no configuration exists in the repository,
        /// a default configuration is created and returned, but not saved to the repository.
        /// To save the default configuration, use <see cref="UpdateRouterConfigAsync"/>.
        /// </remarks>
        public async Task<RouterConfig> GetRouterConfigAsync(CancellationToken cancellationToken = default)
        {
            var config = await _repository.GetRouterConfigAsync(cancellationToken);
            return config ?? CreateDefaultConfig();
        }

        /// <summary>
        /// Updates the router configuration in the repository and reinitializes the router.
        /// </summary>
        /// <param name="config">The new router configuration to save and apply.</param>
        /// <param name="cancellationToken">A token to cancel the operation if needed.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the config parameter is null.</exception>
        /// <remarks>
        /// <para>
        /// This method performs the following steps:
        /// </para>
        /// <list type="number">
        ///   <item><description>Validates the configuration</description></item>
        ///   <item><description>Saves the configuration to the repository</description></item>
        ///   <item><description>Reinitializes the router with the new configuration if it's a DefaultLLMRouter</description></item>
        /// </list>
        /// <para>
        /// Note: This method requires the router to be of type <see cref="DefaultLLMRouter"/>
        /// to apply the configuration at runtime. If the router is of a different type,
        /// the configuration will be saved but not applied until the application restarts.
        /// </para>
        /// </remarks>
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
        /// Adds or updates a model deployment in the router configuration.
        /// </summary>
        /// <param name="deployment">The model deployment to add or update.</param>
        /// <param name="cancellationToken">A token to cancel the operation if needed.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the deployment parameter is null.</exception>
        /// <remarks>
        /// <para>
        /// This method performs the following steps:
        /// </para>
        /// <list type="number">
        ///   <item><description>Validates the deployment information</description></item>
        ///   <item><description>Retrieves the current router configuration</description></item>
        ///   <item><description>Removes any existing deployment with the same name</description></item>
        ///   <item><description>Adds the new deployment to the configuration</description></item>
        ///   <item><description>Saves the updated configuration</description></item>
        ///   <item><description>Reinitializes the router with the new configuration</description></item>
        /// </list>
        /// <para>
        /// If a deployment with the same name already exists, it will be replaced with the new deployment.
        /// The deployment name comparison is case-insensitive.
        /// </para>
        /// </remarks>
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
        /// Updates an existing model deployment in the router configuration.
        /// </summary>
        /// <param name="deployment">The updated model deployment information.</param>
        /// <param name="cancellationToken">A token to cancel the operation if needed.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the deployment parameter is null.</exception>
        /// <remarks>
        /// This method is a convenience alias for <see cref="AddModelDeploymentAsync"/>.
        /// It calls AddModelDeploymentAsync, which will replace any existing deployment with
        /// the same name. See the documentation for AddModelDeploymentAsync for more details.
        /// </remarks>
        public async Task UpdateModelDeploymentAsync(ModelDeployment deployment, CancellationToken cancellationToken = default)
        {
            await AddModelDeploymentAsync(deployment, cancellationToken);
        }

        /// <summary>
        /// Removes a model deployment from the router configuration.
        /// </summary>
        /// <param name="deploymentName">The name of the deployment to remove.</param>
        /// <param name="cancellationToken">A token to cancel the operation if needed.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when the deploymentName parameter is null or empty.</exception>
        /// <remarks>
        /// <para>
        /// This method performs the following steps:
        /// </para>
        /// <list type="number">
        ///   <item><description>Validates the deployment name</description></item>
        ///   <item><description>Retrieves the current router configuration</description></item>
        ///   <item><description>Removes the deployment with the matching name (case-insensitive)</description></item>
        ///   <item><description>If a deployment was removed, saves the updated configuration</description></item>
        ///   <item><description>Reinitializes the router with the new configuration</description></item>
        /// </list>
        /// <para>
        /// If no deployment with the specified name exists, no changes will be made.
        /// </para>
        /// </remarks>
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
        /// Gets all available model deployments from the router configuration.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation if needed.</param>
        /// <returns>
        /// A list of all model deployments defined in the configuration.
        /// Returns an empty list if no configuration exists or no deployments are defined.
        /// </returns>
        /// <remarks>
        /// This method retrieves the model deployments from the persisted configuration
        /// in the repository, not from the runtime router state. This means that if the
        /// router has been modified in memory without saving the configuration, this
        /// method will not reflect those changes.
        /// </remarks>
        public async Task<List<ModelDeployment>> GetModelDeploymentsAsync(CancellationToken cancellationToken = default)
        {
            var config = await _repository.GetRouterConfigAsync(cancellationToken);
            return config?.ModelDeployments ?? new List<ModelDeployment>();
        }

        /// <summary>
        /// Sets or removes fallback models for a primary model in the router configuration.
        /// </summary>
        /// <param name="primaryModel">The name of the primary model to configure fallbacks for.</param>
        /// <param name="fallbacks">A list of fallback model names, or null/empty to remove fallbacks.</param>
        /// <param name="cancellationToken">A token to cancel the operation if needed.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when the primaryModel parameter is null or empty.</exception>
        /// <remarks>
        /// <para>
        /// Fallback models provide high availability by offering alternative models to try
        /// if the primary model is unavailable or fails. This method allows configuring which
        /// models should be used as fallbacks for a specific primary model.
        /// </para>
        /// <para>
        /// When fallbacks is null or empty, any existing fallback configuration for the
        /// primary model will be removed.
        /// </para>
        /// <para>
        /// This method updates both the persisted configuration in the repository and
        /// the runtime router state if using a DefaultLLMRouter.
        /// </para>
        /// </remarks>
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
            if (fallbacks == null || fallbacks.Count() == 0)
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
                if (fallbacks == null || fallbacks.Count() == 0)
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
        /// Gets the configured fallback models for a primary model.
        /// </summary>
        /// <param name="primaryModel">The name of the primary model to get fallbacks for.</param>
        /// <param name="cancellationToken">A token to cancel the operation if needed.</param>
        /// <returns>
        /// A list of fallback model names for the specified primary model.
        /// Returns an empty list if no fallbacks are configured for the model.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when the primaryModel parameter is null or empty.</exception>
        /// <remarks>
        /// <para>
        /// Fallback models provide high availability by offering alternative models to try
        /// if the primary model is unavailable or fails. This method retrieves the currently
        /// configured fallback models for a specific primary model.
        /// </para>
        /// <para>
        /// This method retrieves the fallback configuration from the persisted configuration
        /// in the repository, not from the runtime router state.
        /// </para>
        /// </remarks>
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
        /// Creates a default router configuration with sensible defaults.
        /// </summary>
        /// <returns>A new RouterConfig object with default settings.</returns>
        /// <remarks>
        /// <para>
        /// This method creates a new RouterConfig with the following default settings:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>DefaultRoutingStrategy: "simple"</description></item>
        ///   <item><description>MaxRetries: 3</description></item>
        ///   <item><description>RetryBaseDelayMs: 500</description></item>
        ///   <item><description>RetryMaxDelayMs: 10000</description></item>
        ///   <item><description>Empty model deployments list</description></item>
        ///   <item><description>Empty fallbacks dictionary</description></item>
        /// </list>
        /// <para>
        /// This configuration is used when no existing configuration is found in the repository.
        /// </para>
        /// </remarks>
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
