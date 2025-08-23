using System.Collections.Concurrent;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Routing;
using ConduitLLM.Core.Validation;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Routing
{
    /// <summary>
    /// Context class for tracking attempt information during request retry logic in the LLM router.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The AttemptContext class encapsulates the mutable state used during the retry process
    /// when executing LLM requests. This includes the current attempt count and the last exception
    /// that occurred during request processing.
    /// </para>
    /// <para>
    /// This class was introduced to replace ref parameters in async methods, as C# does not allow
    /// ref parameters in async methods. Using this context object allows for cleaner and more
    /// maintainable code while preserving the state across multiple retry attempts.
    /// </para>
    /// <para>
    /// The router's retry logic uses this context to track how many attempts have been made
    /// and what errors have occurred, allowing for intelligent decisions about whether to
    /// retry a request, use a fallback model, or fail with an appropriate error message.
    /// </para>
    /// </remarks>
    /// <seealso cref="DefaultLLMRouter.ExecuteWithRetriesAsync{T}"/>
    /// <seealso cref="DefaultLLMRouter.AttemptRequestExecutionAsync{T}"/>
    /// <seealso cref="DefaultLLMRouter.TryExecuteRequestAsync{T}"/>
    public class AttemptContext
    {
        /// <summary>
        /// Gets or sets the current attempt count for a request execution.
        /// </summary>
        /// <remarks>
        /// This counter starts at 0 and is incremented for each retry attempt.
        /// The router uses this value to determine when the maximum number of retries
        /// has been reached and to calculate the appropriate backoff delay between retries.
        /// </remarks>
        public int AttemptCount { get; set; }

        /// <summary>
        /// Gets or sets the last exception encountered during request attempts.
        /// </summary>
        /// <remarks>
        /// This property stores the most recent exception that occurred during request execution.
        /// It's used by the router to determine whether the error is recoverable and should be
        /// retried, or if it's a permanent failure that should be reported to the caller.
        /// If multiple attempts fail, this will contain the exception from the most recent attempt.
        /// </remarks>
        public Exception? LastException { get; set; }

        /// <summary>
        /// Creates a new instance of AttemptContext with default values.
        /// </summary>
        /// <remarks>
        /// Initializes a new context with attempt count set to 0 and no last exception.
        /// This represents the state before any execution attempts have been made.
        /// </remarks>
        public AttemptContext()
        {
            AttemptCount = 0;
            LastException = null;
        }
    }

    /// <summary>
    /// Default implementation of the LLM router with multiple routing strategies, 
    /// load balancing, health checking, and fallback support.
    /// </summary>
    /// <remarks>
    /// The DefaultLLMRouter provides sophisticated request routing capabilities for LLM requests:
    /// 
    /// - Multiple routing strategies (simple, round-robin, least cost, etc.)
    /// - Automatic health checking and unhealthy model avoidance
    /// - Fallback support for handling model failures
    /// - Retry logic with exponential backoff for recoverable errors
    /// - Real-time metrics tracking for models (usage count, latency, etc.)
    /// 
    /// The router maintains an internal registry of model deployments and their current
    /// health status, and can automatically route requests to the most appropriate
    /// model based on the selected strategy.
    /// 
    /// This class is split into partial classes for better organization:
    /// - DefaultLLMRouter.cs (core infrastructure and utilities)
    /// - DefaultLLMRouter.ChatCompletion.cs (chat completion functionality)
    /// - DefaultLLMRouter.Streaming.cs (streaming functionality)
    /// - DefaultLLMRouter.Embedding.cs (embedding functionality)
    /// - DefaultLLMRouter.ModelSelection.cs (model selection and routing strategies)
    /// </remarks>
    public partial class DefaultLLMRouter : ILLMRouter
    {
        private readonly ILLMClientFactory _clientFactory;
        private readonly ILogger<DefaultLLMRouter> _logger;
        private readonly IModelCapabilityDetector? _capabilityDetector;
        private readonly IEmbeddingCache? _embeddingCache;
        private readonly MinimalParameterValidator? _parameterValidator;


        /// <summary>
        /// Maps primary models to their list of fallback models
        /// </summary>
        private readonly ConcurrentDictionary<string, List<string>> _fallbackModels = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Tracks the usage count of each model for load balancing purposes
        /// </summary>
        private readonly ConcurrentDictionary<string, int> _modelUsageCount = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Stores the model deployment information for all registered models
        /// </summary>
        private readonly ConcurrentDictionary<string, ModelDeployment> _modelDeployments = new(StringComparer.OrdinalIgnoreCase);

        private readonly Random _random = new();
        private readonly object _lockObject = new();

        private string _defaultRoutingStrategy = "simple";
        private int _maxRetries = 3;
        private int _retryBaseDelayMs = 500;
        private int _retryMaxDelayMs = 10000;

        /// <summary>
        /// Creates a new DefaultLLMRouter instance
        /// </summary>
        /// <param name="clientFactory">Factory for creating LLM clients</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="capabilityDetector">Optional detector for model capabilities like vision support</param>
        /// <param name="embeddingCache">Optional cache for embedding responses</param>
        /// <param name="parameterValidator">Optional validator for request parameters</param>
        public DefaultLLMRouter(
            ILLMClientFactory clientFactory,
            ILogger<DefaultLLMRouter> logger,
            IModelCapabilityDetector? capabilityDetector = null,
            IEmbeddingCache? embeddingCache = null,
            MinimalParameterValidator? parameterValidator = null)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _capabilityDetector = capabilityDetector;
            _embeddingCache = embeddingCache;
            _parameterValidator = parameterValidator;
        }

        /// <summary>
        /// Creates a new DefaultLLMRouter instance with the specified configuration
        /// </summary>
        /// <param name="clientFactory">Factory for creating LLM clients</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="config">Router configuration</param>
        /// <param name="capabilityDetector">Optional detector for model capabilities like vision support</param>
        /// <param name="embeddingCache">Optional cache for embedding responses</param>
        /// <param name="parameterValidator">Optional validator for request parameters</param>
        public DefaultLLMRouter(
            ILLMClientFactory clientFactory,
            ILogger<DefaultLLMRouter> logger,
            RouterConfig config,
            IModelCapabilityDetector? capabilityDetector = null,
            IEmbeddingCache? embeddingCache = null,
            MinimalParameterValidator? parameterValidator = null)
            : this(clientFactory, logger, capabilityDetector, embeddingCache, parameterValidator)
        {
            Initialize(config);
        }

        /// <summary>
        /// Initializes the router with the specified configuration
        /// </summary>
        /// <param name="config">Router configuration</param>
        public void Initialize(RouterConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            _logger.LogInformation("Initializing router with {ModelCount} deployments",
                config.ModelDeployments?.Count ?? 0);

            // Set router configuration values
            _defaultRoutingStrategy = config.DefaultRoutingStrategy;
            _maxRetries = config.MaxRetries;
            _retryBaseDelayMs = config.RetryBaseDelayMs;
            _retryMaxDelayMs = config.RetryMaxDelayMs;

            // Clear existing deployment information
            _modelDeployments.Clear();
            _fallbackModels.Clear();

            // Load model deployments
            if (config.ModelDeployments != null)
            {
                foreach (var deployment in config.ModelDeployments)
                {
                    if (string.IsNullOrWhiteSpace(deployment.DeploymentName) ||
                        string.IsNullOrWhiteSpace(deployment.ModelAlias))
                    {
                        _logger.LogWarning("Skipping deployment with missing name or model alias");
                        continue;
                    }

                    _modelDeployments[deployment.DeploymentName] = deployment;
                    _logger.LogInformation("Added model deployment {DeploymentName} for model {ModelAlias}",
                        deployment.DeploymentName, deployment.ModelAlias);
                }
            }

            // Set up fallbacks
            if (config.Fallbacks != null)
            {
                foreach (var fallbackEntry in config.Fallbacks)
                {
                    if (string.IsNullOrWhiteSpace(fallbackEntry.Key) || fallbackEntry.Value == null)
                    {
                        continue;
                    }

                    AddFallbackModels(fallbackEntry.Key, fallbackEntry.Value);
                }
            }

            _logger.LogInformation("Router initialized with {DeploymentCount} deployments and {FallbackCount} fallback configurations",
                _modelDeployments.Count, _fallbackModels.Count);
        }

        // Chat completion methods implemented in DefaultLLMRouter.ChatCompletion.cs
        // Streaming methods implemented in DefaultLLMRouter.Streaming.cs
        // Embedding methods implemented in DefaultLLMRouter.Embedding.cs
        // Model selection methods implemented in DefaultLLMRouter.ModelSelection.cs

        /// <inheritdoc/>
        public Task<IReadOnlyList<ModelInfo>> GetAvailableModelDetailsAsync(
             CancellationToken cancellationToken = default)
        {
            // Construct ModelInfo list from the internal _modelDeployments dictionary
            IReadOnlyList<ModelInfo> modelInfos = _modelDeployments.Values
                .Where(d => d.IsEnabled) // Optionally filter only enabled deployments
                .Select(deployment => new ModelInfo
                {
                    // Map ModelDeployment properties to ModelInfo properties
                    Id = deployment.ModelName, // Use ModelName (deployment name) as the ID
                    OwnedBy = deployment.ProviderName, // Use ProviderName as OwnedBy
                    MaxContextTokens = null // Context window info not directly in ModelDeployment
                                            // Could potentially be fetched from client or config if needed
                                            // Object property defaults to "model" in ModelInfo class
                })
                .ToList();

            _logger.LogInformation("Retrieved {Count} available model details.", modelInfos.Count);

            // Return as a completed task since the operation is synchronous
            return Task.FromResult<IReadOnlyList<ModelInfo>>(modelInfos);
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> GetAvailableModels()
        {
            // Return all registered deployments
            return _modelDeployments.Keys.ToList();
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> GetFallbackModels(string modelName)
        {
            if (_fallbackModels.TryGetValue(modelName, out var fallbacks))
            {
                return fallbacks.ToList();
            }
            return Array.Empty<string>();
        }


        /// <summary>
        /// Add fallback models for a primary model
        /// </summary>
        /// <param name="primaryModel">The primary model name</param>
        /// <param name="fallbacks">List of fallback model names</param>
        public void AddFallbackModels(string primaryModel, IEnumerable<string> fallbacks)
        {
            if (string.IsNullOrEmpty(primaryModel) || fallbacks == null)
            {
                return;
            }

            _fallbackModels[primaryModel] = new List<string>(fallbacks);
            _logger.LogInformation("Added {FallbackCount} fallback models for {PrimaryModel}",
                _fallbackModels[primaryModel].Count, primaryModel);
        }

        /// <summary>
        /// Removes fallbacks for a primary model
        /// </summary>
        /// <param name="primaryModel">The primary model name</param>
        public void RemoveFallbacks(string primaryModel)
        {
            if (_fallbackModels.TryRemove(primaryModel, out _))
            {
                _logger.LogInformation("Removed fallbacks for {PrimaryModel}", primaryModel);
            }
        }

        /// <summary>
        /// Reset usage statistics for all models
        /// </summary>
        public void ResetUsageStatistics()
        {
            _modelUsageCount.Clear();

            // Reset usage metrics in model deployments
            foreach (var deployment in _modelDeployments.Values)
            {
                deployment.RequestCount = 0;
                deployment.LastUsed = DateTime.MinValue;
            }

            _logger.LogInformation("Reset all model usage statistics");
        }

        #region Shared Utility Methods

        /// <summary>
        /// Determines if retry attempts should stop based on the error type and retry count.
        /// </summary>
        private bool ShouldStopRetrying(AttemptContext attemptContext, int retryAttempt)
        {
            // If this is a non-recoverable error, don't retry
            if (!IsRecoverableError(attemptContext.LastException))
            {
                _logger.LogWarning("Non-recoverable error encountered, stopping retry attempts");
                return true;
            }

            // If this is the last retry, don't continue
            if (retryAttempt >= _maxRetries)
            {
                _logger.LogWarning("Maximum retry attempts reached");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Applies a delay before the next retry attempt.
        /// </summary>
        /// <param name="attemptCount">The current attempt count.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous delay operation.</returns>
        private async Task ApplyRetryDelayAsync(int attemptCount, CancellationToken cancellationToken)
        {
            int delayMs = CalculateBackoffDelay(attemptCount);

            _logger.LogInformation(
                "Retrying request in {DelayMs}ms (attempt {CurrentAttempt}/{MaxRetries})",
                delayMs, attemptCount, _maxRetries);

            await Task.Delay(delayMs, cancellationToken);
        }

        /// <summary>
        /// Handles exceptions that occur during request execution.
        /// </summary>
        /// <param name="exception">The exception that occurred.</param>
        /// <param name="selectedModel">The model that was used.</param>
        private void HandleExecutionException(Exception exception, string selectedModel)
        {
            _logger.LogWarning(exception, "Request to model {ModelName} failed",
                selectedModel);
        }

        /// <summary>
        /// Updates all statistics for a model after successful request completion.
        /// </summary>
        /// <param name="modelName">The name of the model.</param>
        /// <param name="latencyMs">The request latency in milliseconds.</param>
        private void UpdateModelStatistics(string modelName, long latencyMs)
        {
            IncrementModelUsage(modelName);
            UpdateModelLatency(modelName, latencyMs);
        }

        /// <summary>
        /// Increments the usage count for a model
        /// </summary>
        private void IncrementModelUsage(string modelName)
        {
            _modelUsageCount.AddOrUpdate(
                modelName,
                1,
                (_, count) => count + 1);

            // Update the model deployment if available
            if (_modelDeployments.TryGetValue(modelName, out var deployment))
            {
                deployment.RequestCount++;
                deployment.LastUsed = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Updates the latency statistics for a model
        /// </summary>
        private void UpdateModelLatency(string modelName, long latencyMs)
        {
            if (_modelDeployments.TryGetValue(modelName, out var deployment))
            {
                // Calculate running average
                if (deployment.RequestCount <= 1)
                {
                    deployment.AverageLatencyMs = latencyMs;
                }
                else
                {
                    // Simple exponential moving average with 0.1 weight for new value
                    deployment.AverageLatencyMs = (0.9 * deployment.AverageLatencyMs) + (0.1 * latencyMs);
                }
            }
        }

        /// <summary>
        /// Calculates the backoff delay for retries using exponential backoff
        /// </summary>
        private int CalculateBackoffDelay(int attemptCount)
        {
            // Calculate exponential backoff with jitter
            double backoffFactor = Math.Pow(2, attemptCount - 1);
            int baseDelay = (int)(_retryBaseDelayMs * backoffFactor);
            int jitter = _random.Next(0, baseDelay / 4);
            int delay = baseDelay + jitter;

            // Cap at max delay
            return Math.Min(delay, _retryMaxDelayMs);
        }

        /// <summary>
        /// Determines if an error is recoverable (should be retried)
        /// </summary>
        private bool IsRecoverableError(Exception? ex)
        {
            // If exception is null, treat it as non-recoverable
            if (ex == null)
            {
                return false;
            }

            // Categorize exception types as recoverable or not
            // Some errors should not be retried as they will always fail (e.g., validation errors)
            return ex switch
            {
                LLMCommunicationException => true,       // Network errors can be retried
                TimeoutException => true,                // Timeouts can be retried
                OperationCanceledException => false,     // Cancellations should not be retried
                ArgumentException => false,              // Invalid arguments won't be fixed by retry
                ConfigurationException => false,         // Configuration issues won't be fixed by retry
                InvalidOperationException => false,      // Logical errors won't be fixed by retry
                _ => true                                // Default to retry for unknown exceptions
            };
        }

        #endregion
    }
}