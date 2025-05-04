using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Routing;
using ConduitLLM.Core.Routing.Strategies;

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
    /// </remarks>
    public class DefaultLLMRouter : ILLMRouter
    {
        private readonly ILLMClientFactory _clientFactory;
        private readonly ILogger<DefaultLLMRouter> _logger;
        
        /// <summary>
        /// Tracks the health status (true = healthy, false = unhealthy) of each model
        /// </summary>
        private readonly ConcurrentDictionary<string, bool> _modelHealthStatus = new(StringComparer.OrdinalIgnoreCase);
        
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
        public DefaultLLMRouter(ILLMClientFactory clientFactory, ILogger<DefaultLLMRouter> logger)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates a new DefaultLLMRouter instance with the specified configuration
        /// </summary>
        /// <param name="clientFactory">Factory for creating LLM clients</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="config">Router configuration</param>
        public DefaultLLMRouter(ILLMClientFactory clientFactory, ILogger<DefaultLLMRouter> logger, RouterConfig config)
            : this(clientFactory, logger)
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
            _modelHealthStatus.Clear();
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
                    _modelHealthStatus[deployment.DeploymentName] = deployment.IsHealthy;
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
                } // Closing brace for foreach
            } // Closing brace for if (config.Fallbacks != null)

            _logger.LogInformation("Router initialized with {DeploymentCount} deployments and {FallbackCount} fallback configurations",
                _modelDeployments.Count, _fallbackModels.Count);
        } // Closing brace for Initialize method

        /// <inheritdoc/>
        public async Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? routingStrategy = null,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            
            // Determine routing strategy
            var strategy = DetermineRoutingStrategy(routingStrategy);
            string? originalModelRequested = request.Model;
            
            _logger.LogDebug("Processing chat completion request using {Strategy} strategy", strategy);
            
            // Check for passthrough mode first
            if (ShouldUsePassthroughMode(request, strategy))
            {
                _logger.LogDebug("Using passthrough mode for model {Model}", request.Model);
                return await DirectModelPassthroughAsync(request, apiKey, cancellationToken);
            }
            
            // Otherwise use normal routing with retries
            return await RouteThroughLoadBalancerAsync(request, originalModelRequested, strategy, apiKey, cancellationToken);
        }
        
        /// <summary>
        /// Determines the routing strategy to use based on input and defaults.
        /// </summary>
        /// <param name="requestedStrategy">The strategy requested, or null to use default.</param>
        /// <returns>The strategy name to use for routing.</returns>
        private string DetermineRoutingStrategy(string? requestedStrategy)
        {
            return requestedStrategy ?? _defaultRoutingStrategy;
        }
        
        /// <summary>
        /// Determines if a request should be handled in passthrough mode.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="strategy">The routing strategy.</param>
        /// <returns>True if the request should be handled in passthrough mode, false otherwise.</returns>
        private bool ShouldUsePassthroughMode(ChatCompletionRequest request, string strategy)
        {
            return !string.IsNullOrEmpty(request.Model) &&
                   strategy.Equals("passthrough", StringComparison.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Directly passes the request to the specified model without routing.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="apiKey">Optional API key to use for the request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The chat completion response.</returns>
        private async Task<ChatCompletionResponse> DirectModelPassthroughAsync(
            ChatCompletionRequest request,
            string? apiKey, 
            CancellationToken cancellationToken)
        {
            // This is just a renamed version of HandlePassthroughRequestAsync for clarity
            try
            {
                var client = _clientFactory.GetClient(request.Model);
                return await client.CreateChatCompletionAsync(request, apiKey, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during pass-through to model {Model}", request.Model);
                UpdateModelHealth(request.Model, false);
                throw;
            }
        }
        
        /// <summary>
        /// Routes a request through the load balancer with retry logic.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="originalModel">The original model requested.</param>
        /// <param name="strategy">The routing strategy to use.</param>
        /// <param name="apiKey">Optional API key to use for the request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The chat completion response.</returns>
        /// <exception cref="LLMCommunicationException">Thrown when all attempts fail due to communication errors.</exception>
        /// <exception cref="ModelUnavailableException">Thrown when no suitable model is available.</exception>
        private async Task<ChatCompletionResponse> RouteThroughLoadBalancerAsync(
            ChatCompletionRequest request,
            string? originalModel,
            string strategy,
            string? apiKey,
            CancellationToken cancellationToken)
        {
            List<string> attemptedModels = new();
            var attemptContext = new AttemptContext();
            
            // Attempt to execute the request with retries
            var result = await ExecuteWithRetriesAsync(
                request, 
                originalModel,
                strategy, 
                attemptedModels, 
                attemptContext,
                apiKey, 
                cancellationToken);
                
            if (result != null)
            {
                return result;
            }
            
            // Handle the case where all attempts have failed
            HandleFailedAttempts(attemptContext.LastException, originalModel, attemptedModels, attemptContext.AttemptCount);
            
            // This line will never be reached, but is required for compilation
            throw new ModelUnavailableException(
                $"No suitable model found for {originalModel} after {attemptContext.AttemptCount} attempts");
        }

        // Removed redundant HandlePassthroughRequestAsync method as it's been replaced by DirectModelPassthroughAsync

        /// <summary>
        /// Executes a chat completion request with retry logic and fallback handling.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="originalModelRequested">The original model name requested.</param>
        /// <param name="strategy">The routing strategy to use.</param>
        /// <param name="attemptedModels">List of models that have already been attempted.</param>
        /// <param name="attemptContext">Context object holding attempt count and exception details.</param>
        /// <param name="apiKey">Optional API key to use for the request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The chat completion response if successful, null otherwise.</returns>
        /// <remarks>
        /// <para>
        /// This method handles the core retry logic for LLM requests, tracking attempted models
        /// and managing backoff delays between attempts. It uses the <see cref="AttemptContext"/>
        /// to keep track of the current state of the retry process.
        /// </para>
        /// <para>
        /// For each retry attempt, the method:
        /// 1. Updates the attempt count in the context
        /// 2. Selects an appropriate model based on the routing strategy
        /// 3. Executes the request with that model
        /// 4. If successful, returns the result
        /// 5. If unsuccessful but the error is recoverable, applies a delay and retries
        /// 6. If the error is not recoverable or max retries reached, returns null
        /// </para>
        /// </remarks>
        private async Task<ChatCompletionResponse?> ExecuteWithRetriesAsync(
            ChatCompletionRequest request,
            string? originalModelRequested,
            string strategy,
            List<string> attemptedModels,
            AttemptContext attemptContext,
            string? apiKey,
            CancellationToken cancellationToken)
        {
            for (int retryAttempt = 1; retryAttempt <= _maxRetries; retryAttempt++)
            {
                // Update attempt counter in context
                attemptContext.AttemptCount = retryAttempt;
                
                // Attempt the request execution with a specific model
                var result = await TryRequestExecutionWithSelectedModelAsync(
                    request,
                    originalModelRequested,
                    strategy,
                    attemptedModels,
                    attemptContext,
                    apiKey,
                    cancellationToken);
                
                // If successful, return the result
                if (result != null)
                {
                    return result;
                }
                
                // Check if we should continue retrying
                if (ShouldStopRetrying(attemptContext, retryAttempt))
                {
                    break;
                }
                
                // Apply backoff delay before next retry
                await ApplyRetryDelayAsync(retryAttempt, cancellationToken);
            }
            
            return null;
        }
        
        /// <summary>
        /// Attempts to execute a request with a selected model.
        /// </summary>
        private async Task<ChatCompletionResponse?> TryRequestExecutionWithSelectedModelAsync(
            ChatCompletionRequest request,
            string? originalModelRequested,
            string strategy,
            List<string> attemptedModels,
            AttemptContext attemptContext,
            string? apiKey,
            CancellationToken cancellationToken)
        {
            // This method is a renamed version of AttemptRequestExecutionAsync for clarity
            return await AttemptRequestExecutionAsync(
                request,
                originalModelRequested,
                strategy,
                attemptedModels,
                attemptContext,
                apiKey,
                cancellationToken);
        }
        
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
        /// Attempts to execute a request with a dynamically selected model.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="originalModelRequested">The original model name requested.</param>
        /// <param name="strategy">The routing strategy to use.</param>
        /// <param name="attemptedModels">List of models that have already been attempted.</param>
        /// <param name="attemptContext">Context object holding attempt count and exception tracking information.</param>
        /// <param name="apiKey">Optional API key to use for the request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The chat completion response if successful, null otherwise.</returns>
        /// <remarks>
        /// <para>
        /// This method represents a single attempt to execute a request during the retry process.
        /// It selects an appropriate model based on the routing strategy and models that haven't
        /// been tried yet, then attempts to execute the request with that model.
        /// </para>
        /// <para>
        /// It updates the <paramref name="attemptedModels"/> list to track which models have been tried,
        /// which ensures we don't retry with the same model if it failed previously.
        /// </para>
        /// <para>
        /// This method works with the <see cref="AttemptContext"/> to maintain state between retries,
        /// but does not directly update the attempt count (that's managed by <see cref="ExecuteWithRetriesAsync"/>).
        /// </para>
        /// </remarks>
        private async Task<ChatCompletionResponse?> AttemptRequestExecutionAsync(
            ChatCompletionRequest request,
            string? originalModelRequested,
            string strategy,
            List<string> attemptedModels,
            AttemptContext attemptContext,
            string? apiKey,
            CancellationToken cancellationToken)
        {
            // Get the next model based on strategy
            string? selectedModel = await SelectModelAsync(originalModelRequested, strategy, attemptedModels, cancellationToken);
            
            if (selectedModel == null)
            {
                _logger.LogWarning("No suitable model found");
                return null;
            }
            
            _logger.LogInformation("Selected model {ModelName} for request using {Strategy} strategy",
                selectedModel, strategy);
                
            // Add this model to the list of attempted ones
            attemptedModels.Add(selectedModel);
            
            // Try to execute with the selected model
            return await TryExecuteRequestAsync(
                request, 
                selectedModel, 
                attemptContext, 
                apiKey, 
                cancellationToken);
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
        /// Attempts to execute a chat completion request with a specific model and tracks any exceptions.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="selectedModel">The model to use for the request.</param>
        /// <param name="attemptContext">Context object to track attempts and capture exceptions.</param>
        /// <param name="apiKey">Optional API key to use for the request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The chat completion response if successful, null otherwise.</returns>
        /// <remarks>
        /// <para>
        /// This method performs the actual execution of the LLM request with a specific model.
        /// It modifies the request's model property to use the selected model, then attempts to 
        /// execute the request.
        /// </para>
        /// <para>
        /// If the execution succeeds, it returns the response. If it fails with an exception,
        /// it stores the exception in the <see cref="AttemptContext.LastException"/> property
        /// for analysis by the retry logic, marks the model as unhealthy if appropriate,
        /// and returns null to indicate failure.
        /// </para>
        /// <para>
        /// This method represents the innermost layer of the retry mechanism, with
        /// <see cref="AttemptRequestExecutionAsync"/> and <see cref="ExecuteWithRetriesAsync"/>
        /// providing the higher-level retry and model selection logic.
        /// </para>
        /// </remarks>
        private async Task<ChatCompletionResponse?> TryExecuteRequestAsync(
            ChatCompletionRequest request,
            string selectedModel,
            AttemptContext attemptContext,
            string? apiKey,
            CancellationToken cancellationToken)
        {
            // Apply the selected model
            request.Model = GetModelAliasForDeployment(selectedModel);

            try
            {
                return await ExecuteModelRequestAsync(request, selectedModel, apiKey, cancellationToken);
            }
            catch (Exception ex)
            {
                HandleExecutionException(ex, selectedModel);
                attemptContext.LastException = ex;
                return null;
            }
        }
        
        /// <summary>
        /// Executes a request with the specified model and tracks metrics.
        /// </summary>
        /// <param name="request">The chat completion request with model set.</param>
        /// <param name="selectedModel">The model to use for tracking metrics.</param>
        /// <param name="apiKey">Optional API key to use for the request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The chat completion response.</returns>
        private async Task<ChatCompletionResponse> ExecuteModelRequestAsync(
            ChatCompletionRequest request,
            string selectedModel,
            string? apiKey,
            CancellationToken cancellationToken)
        {
            // Track execution time for metrics
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                // Get the client for this model and execute the request
                var client = _clientFactory.GetClient(request.Model);
                var result = await client.CreateChatCompletionAsync(request, apiKey, cancellationToken);

                stopwatch.Stop();

                // Update model stats on success
                UpdateModelStatistics(selectedModel, stopwatch.ElapsedMilliseconds);

                return result;
            }
            catch (Exception)
            {
                stopwatch.Stop();
                throw; // Re-throw to be handled by the caller
            }
        }
        
        /// <summary>
        /// Handles exceptions that occur during request execution.
        /// </summary>
        /// <param name="exception">The exception that occurred.</param>
        /// <param name="selectedModel">The model that was used.</param>
        private void HandleExecutionException(Exception exception, string selectedModel)
        {
            _logger.LogWarning(exception, "Request to model {ModelName} failed, marking as unhealthy",
                selectedModel);

            // Mark this model as unhealthy
            UpdateModelHealth(selectedModel, false);
        }

        /// <summary>
        /// Handles the case where all attempts to execute a request have failed.
        /// </summary>
        /// <param name="lastException">The last exception that occurred.</param>
        /// <param name="originalModelRequested">The original model name requested.</param>
        /// <param name="attemptedModels">List of models that were attempted.</param>
        /// <param name="attemptCount">The number of attempts that were made.</param>
        private void HandleFailedAttempts(
            Exception? lastException, 
            string? originalModelRequested,
            List<string> attemptedModels,
            int attemptCount)
        {
            if (lastException != null)
            {
                _logger.LogError(lastException,
                    "All attempts failed for model {OriginalModel} after trying {ModelCount} models with {AttemptCount} attempts",
                    originalModelRequested, attemptedModels.Count, attemptCount);
                
                throw new LLMCommunicationException(
                    $"Failed to process request after {attemptCount} attempts across {attemptedModels.Count} models",
                    lastException);
            }

            throw new ModelUnavailableException(
                $"No suitable model found for {originalModelRequested} after {attemptCount} attempts");
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? routingStrategy = null,
            string? apiKey = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            // We need to handle streaming differently due to yield return limitations
            var strategy = routingStrategy ?? _defaultRoutingStrategy;
            
            // First, select the appropriate model
            string selectedModel = await SelectModelForStreamingRequestAsync(request, strategy, cancellationToken);
            
            // Update the request with the selected model
            request.Model = GetModelAliasForDeployment(selectedModel);
            
            // Process the streaming request
            await foreach (var chunk in ProcessStreamingRequestAsync(request, selectedModel, apiKey, cancellationToken))
            {
                yield return chunk;
            }
        }
        
        /// <summary>
        /// Selects the appropriate model for a streaming request.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="strategy">The routing strategy to use.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The selected model name.</returns>
        /// <exception cref="ModelUnavailableException">Thrown when no suitable model is found.</exception>
        private async Task<string> SelectModelForStreamingRequestAsync(
            ChatCompletionRequest request,
            string strategy,
            CancellationToken cancellationToken)
        {
            string? modelToUse = null;

            // If we're using a passthrough strategy and have a model, just use it directly
            if (!string.IsNullOrEmpty(request.Model) &&
                strategy.Equals("passthrough", StringComparison.OrdinalIgnoreCase))
            {
                modelToUse = request.Model;
            }
            else
            {
                // Otherwise, select a model using our routing logic
                modelToUse = await SelectModelForStreamingAsync(
                    request.Model, strategy, _maxRetries, cancellationToken);

                if (modelToUse == null)
                {
                    throw new ModelUnavailableException(
                        $"No suitable model found for streaming request with original model {request.Model}");
                }
            }
            
            return modelToUse;
        }
        
        /// <summary>
        /// Processes a streaming request with the selected model.
        /// </summary>
        /// <param name="request">The chat completion request with the model already set.</param>
        /// <param name="selectedModel">The selected model name for metrics and health tracking.</param>
        /// <param name="apiKey">Optional API key to use for the request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An async enumerable of chat completion chunks.</returns>
        /// <exception cref="LLMCommunicationException">Thrown when streaming fails or returns no chunks.</exception>
        private async IAsyncEnumerable<ChatCompletionChunk> ProcessStreamingRequestAsync(
            ChatCompletionRequest request,
            string selectedModel,
            string? apiKey,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            _logger.LogInformation("Streaming from model {ModelName}", selectedModel);

            var client = _clientFactory.GetClient(request.Model);
            IAsyncEnumerable<ChatCompletionChunk> stream;
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                // Get the stream outside of the yield section
                stream = client.StreamChatCompletionAsync(request, apiKey, cancellationToken);
            }
            catch (Exception ex)
            {
                // Handle exceptions during stream creation
                UpdateModelHealth(selectedModel, false);
                _logger.LogError(ex, "Error creating stream from model {ModelName}", selectedModel);
                throw;
            }

            // Now iterate through the stream
            bool receivedAnyChunks = false;

            // We can't use try-catch here, so we'll handle errors at a higher level
            await foreach (var chunk in stream.WithCancellation(cancellationToken))
            {
                receivedAnyChunks = true;
                yield return chunk;
            }

            stopwatch.Stop();

            // After streaming completes, update model statistics
            if (receivedAnyChunks)
            {
                // Success case - update metrics
                UpdateModelStatistics(selectedModel, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                // No chunks received - update health status
                UpdateModelHealth(selectedModel, false);
                throw new LLMCommunicationException($"No chunks received from model {selectedModel}");
            }
        }
        
        /// <summary>
        /// Updates all statistics for a model after successful request completion.
        /// </summary>
        /// <param name="modelName">The name of the model.</param>
        /// <param name="latencyMs">The request latency in milliseconds.</param>
        private void UpdateModelStatistics(string modelName, long latencyMs)
        {
            UpdateModelHealth(modelName, true);
            IncrementModelUsage(modelName);
            UpdateModelLatency(modelName, latencyMs);
        }

        /// <inheritdoc/>
        public Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? routingStrategy = null,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Implementation to be added
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<ModelInfo>> GetAvailableModelDetailsAsync(
             CancellationToken cancellationToken = default)
        {
            // Construct ModelInfo list from the internal _modelDeployments dictionary
            var modelInfos = _modelDeployments.Values
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
                .ToList()
                .AsReadOnly(); // Convert to ReadOnlyList

            _logger.LogInformation("Retrieved {Count} available model details.", modelInfos.Count);

            // Return as a completed task since the operation is synchronous
            return Task.FromResult<IReadOnlyList<ModelInfo>>(modelInfos);
        }

        /// <summary>
        /// Select a model for streaming, handling retries and fallbacks
        /// </summary>
        /// <param name="requestedModel">The model name originally requested by the client, or null if no specific model was requested.</param>
        /// <param name="strategy">The routing strategy to use for selection.</param>
        /// <param name="maxRetries">Maximum number of retry attempts.</param>
        /// <param name="cancellationToken">A token for cancelling the operation.</param>
        /// <returns>The name of the selected model, or null if no suitable model could be found.</returns>
        /// <remarks>
        /// This method specifically handles model selection for streaming requests, with retry logic
        /// to ensure a healthy model is selected. It reuses the core SelectModelAsync method,
        /// which delegates to the strategy pattern implementation.
        /// </remarks>
        private async Task<string?> SelectModelForStreamingAsync(
            string? requestedModel,
            string strategy,
            int maxRetries,
            CancellationToken cancellationToken)
        {
            // Start tracking retry attempt count
            int attemptCount = 0;
            List<string> attemptedModels = new();

            while (attemptCount <= maxRetries)
            {
                attemptCount++;

                // Select a model based on strategy using the same SelectModelAsync method
                // that now delegates to our strategy pattern
                string? selectedModel = await SelectModelAsync(requestedModel, strategy, attemptedModels, cancellationToken);

                if (selectedModel == null)
                {
                    _logger.LogWarning("No suitable model found for streaming after {AttemptsCount} attempts",
                        attemptCount);
                    break;
                }

                _logger.LogInformation("Selected model {ModelName} for streaming using {Strategy} strategy",
                    selectedModel, strategy);

                // Add this model to attempted list regardless of health status
                attemptedModels.Add(selectedModel);

                // If the model is healthy, use it
                if (!_modelHealthStatus.TryGetValue(selectedModel, out var isHealthy) || isHealthy)
                {
                    return selectedModel;
                }

                // Model is unhealthy, try another after a delay
                if (attemptCount <= maxRetries)
                {
                    int delayMs = CalculateBackoffDelay(attemptCount);
                    await Task.Delay(delayMs, cancellationToken);
                }
            }

            // No suitable model found
            return null;
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

        /// <inheritdoc/>
        public void UpdateModelHealth(string modelName, bool isHealthy)
        {
            if (string.IsNullOrEmpty(modelName))
            {
                return;
            }

            _modelHealthStatus[modelName] = isHealthy;

            // Also update the model deployment health if it exists
            if (_modelDeployments.TryGetValue(modelName, out var deployment))
            {
                deployment.IsHealthy = isHealthy;
            }

            _logger.LogInformation("Updated model {ModelName} health status to {IsHealthy}",
                modelName, isHealthy);
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

        #region Private Methods

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
        /// Gets the model alias to use with the client for a deployment
        /// </summary>
        private string GetModelAliasForDeployment(string deploymentName)
        {
            if (_modelDeployments.TryGetValue(deploymentName, out var deployment))
            {
                return deployment.ModelAlias;
            }
            return deploymentName; // Fallback to the deployment name if not found
        }

        /// <summary>
        /// Selects the most appropriate model based on the specified strategy and current system state.
        /// </summary>
        /// <param name="requestedModel">The model name originally requested by the client, or null if no specific model was requested.</param>
        /// <param name="strategy">The routing strategy to use for selection (e.g., "simple", "roundrobin", "leastcost").</param>
        /// <param name="excludeModels">List of model names to exclude from consideration (typically models that have already been attempted).</param>
        /// <param name="cancellationToken">A token for cancelling the operation.</param>
        /// <returns>The name of the selected model, or null if no suitable model could be found.</returns>
        /// <remarks>
        /// This method implements the core model selection logic:
        /// 
        /// 1. Builds a candidate list based on the requested model and available fallbacks
        /// 2. Filters out excluded models and unhealthy models
        /// 3. Gets the appropriate strategy from the factory
        /// 4. Delegates model selection to the strategy implementation
        /// 
        /// If no specific model was requested, it will consider all available models.
        /// If the strategy is not recognized, it defaults to the "simple" strategy.
        /// </remarks>
        private async Task<string?> SelectModelAsync(
            string? requestedModel,
            string strategy,
            List<string> excludeModels,
            CancellationToken cancellationToken)
        {
            // Small delay to make this actually async
            await Task.Delay(1, cancellationToken);

            // Get filtered list of available models
            var (availableModels, availableDeployments) = await GetFilteredAvailableModelsAsync(
                requestedModel, excludeModels, cancellationToken);

            if (!availableModels.Any())
            {
                _logger.LogWarning("No available models found for requestedModel={RequestedModel}", requestedModel);
                return null;
            }

            // Handle passthrough strategy as a special case
            if (IsPassthroughStrategy(strategy))
            {
                return availableModels.FirstOrDefault();
            }

            // Select model using the appropriate strategy
            return SelectModelUsingStrategy(strategy, availableModels, availableDeployments);
        }
        
        /// <summary>
        /// Gets a filtered list of available models based on requested model and exclusions.
        /// </summary>
        private async Task<(List<string> AvailableModels, Dictionary<string, ModelDeployment> AvailableDeployments)> 
            GetFilteredAvailableModelsAsync(string? requestedModel, List<string> excludeModels, CancellationToken cancellationToken)
        {
            // Add small delay to ensure method is truly async
            await Task.Delay(1, cancellationToken);
            
            // Build candidate models list
            var candidateModels = BuildCandidateModelsList(requestedModel, excludeModels);
            
            // Filter to only healthy models
            var availableModels = FilterHealthyModels(candidateModels);
            
            // Get deployment information for available models
            var availableDeployments = GetAvailableDeployments(availableModels);
            
            return (availableModels, availableDeployments);
        }
        
        /// <summary>
        /// Determines if the strategy is a passthrough strategy.
        /// </summary>
        private bool IsPassthroughStrategy(string strategy)
        {
            return strategy.Equals("passthrough", StringComparison.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Selects a model using the appropriate strategy implementation.
        /// </summary>
        private string? SelectModelUsingStrategy(
            string strategy, 
            List<string> availableModels, 
            Dictionary<string, ModelDeployment> availableDeployments)
        {
            // Use the strategy factory to get the appropriate strategy and delegate selection
            var modelSelectionStrategy = ModelSelectionStrategyFactory.GetStrategy(strategy);
            
            _logger.LogDebug("Using {Strategy} strategy to select from {ModelCount} models", 
                strategy, availableModels.Count);
                
            return modelSelectionStrategy.SelectModel(
                availableModels,
                availableDeployments,
                _modelUsageCount);
        }
        
        /// <summary>
        /// Builds a list of candidate models based on the requested model and available fallbacks.
        /// </summary>
        /// <param name="requestedModel">The model name originally requested by the client.</param>
        /// <param name="excludeModels">List of model names to exclude from consideration.</param>
        /// <returns>A list of candidate model names.</returns>
        private List<string> BuildCandidateModelsList(string? requestedModel, List<string> excludeModels)
        {
            List<string> candidateModels = new();

            // If we have a specific requested model and it's not in the excluded list, start with that
            if (!string.IsNullOrEmpty(requestedModel) && !excludeModels.Contains(requestedModel))
            {
                // Find any deployments that correspond to this model alias
                var matchingDeployments = _modelDeployments.Values
                    .Where(d => d.ModelAlias.Equals(requestedModel, StringComparison.OrdinalIgnoreCase))
                    .Select(d => d.DeploymentName)
                    .ToList();

                if (matchingDeployments.Any())
                {
                    candidateModels.AddRange(matchingDeployments);
                }
                else
                {
                    // No matching deployments, treat as deployment name directly
                    candidateModels.Add(requestedModel);
                }

                // Add fallbacks for this model if available
                if (_fallbackModels.TryGetValue(requestedModel, out var fallbacks))
                {
                    candidateModels.AddRange(fallbacks.Where(m => !excludeModels.Contains(m)));
                }
            }

            // If no candidates yet, use all available models
            if (!candidateModels.Any())
            {
                candidateModels = _modelDeployments.Keys
                    .Where(m => !excludeModels.Contains(m))
                    .ToList();
            }
            
            return candidateModels;
        }
        
        /// <summary>
        /// Filters a list of candidate models to include only healthy ones.
        /// </summary>
        /// <param name="candidateModels">The list of candidate model names.</param>
        /// <returns>A filtered list containing only healthy models.</returns>
        private List<string> FilterHealthyModels(List<string> candidateModels)
        {
            return candidateModels
                .Where(m => !_modelHealthStatus.TryGetValue(m, out var healthy) || healthy)
                .ToList();
        }
        
        /// <summary>
        /// Converts a list of model names to a dictionary of their deployment information.
        /// </summary>
        /// <param name="modelNames">The list of model names to convert.</param>
        /// <returns>A dictionary mapping model names to their deployment information.</returns>
        private Dictionary<string, ModelDeployment> GetAvailableDeployments(List<string> modelNames)
        {
            return modelNames
                .Where(m => _modelDeployments.ContainsKey(m))
                .ToDictionary(
                    m => m,
                    m => _modelDeployments[m],
                    StringComparer.OrdinalIgnoreCase);
        }

        // SelectRoundRobin method removed as it's now handled by RoundRobinModelSelectionStrategy

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
    } // Closing brace for DefaultLLMRouter class
} // Closing brace for namespace
