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

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Routing
{
    /// <summary>
    /// Default implementation of the LLM router with load balancing and fallback support
    /// </summary>
    public class DefaultLLMRouter : ILLMRouter
    {
        private readonly ILLMClientFactory _clientFactory;
        private readonly ILogger<DefaultLLMRouter> _logger;
        private readonly ConcurrentDictionary<string, bool> _modelHealthStatus = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, List<string>> _fallbackModels = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, int> _modelUsageCount = new(StringComparer.OrdinalIgnoreCase);
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
                }
            }

            _logger.LogInformation("Router initialized with {DeploymentCount} deployments and {FallbackCount} fallback configurations",
                _modelDeployments.Count, _fallbackModels.Count);
        }

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

            var strategy = routingStrategy ?? _defaultRoutingStrategy;
            
            // Store original model for fallback logic
            string? originalModelRequested = request.Model;
            
            // Start tracking retry attempt count
            int attemptCount = 0;
            List<string> attemptedModels = new();
            Exception? lastException = null;

            // If the model is already specified and we're using passthrough strategy, 
            // just pass through to the normal flow
            if (!string.IsNullOrEmpty(request.Model) && 
                strategy.Equals("passthrough", StringComparison.OrdinalIgnoreCase))
            {
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

            while (attemptCount <= _maxRetries)
            {
                attemptCount++;
                
                // Get the next model based on strategy
                string? selectedModel = await SelectModelAsync(originalModelRequested, strategy, attemptedModels, cancellationToken);
                
                if (selectedModel == null)
                {
                    _logger.LogWarning("No suitable model found after {AttemptsCount} attempts", attemptCount);
                    break;
                }
                
                _logger.LogInformation("Selected model {ModelName} for request using {Strategy} strategy",
                    selectedModel, strategy);
                
                // Add this model to the list of attempted ones
                attemptedModels.Add(selectedModel);
                
                // Apply the selected model
                request.Model = GetModelAliasForDeployment(selectedModel);
                
                try
                {
                    // Track execution time for metrics
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    
                    // Get the client for this model and execute the request
                    var client = _clientFactory.GetClient(request.Model);
                    var result = await client.CreateChatCompletionAsync(request, apiKey, cancellationToken);
                    
                    stopwatch.Stop();
                    
                    // Update model stats
                    UpdateModelHealth(selectedModel, true);
                    IncrementModelUsage(selectedModel);
                    UpdateModelLatency(selectedModel, stopwatch.ElapsedMilliseconds);
                    
                    return result;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogWarning(ex, "Request to model {ModelName} failed, marking as unhealthy", 
                        selectedModel);
                    
                    // Mark this model as unhealthy
                    UpdateModelHealth(selectedModel, false);
                    
                    // If we're out of retries, or if this is a non-recoverable error, don't retry
                    if (attemptCount > _maxRetries || !IsRecoverableError(ex))
                    {
                        break;
                    }
                    
                    // Calculate delay using exponential backoff
                    int delayMs = CalculateBackoffDelay(attemptCount);
                    
                    _logger.LogInformation(
                        "Retrying request in {DelayMs}ms (attempt {CurrentAttempt}/{MaxRetries})",
                        delayMs, attemptCount, _maxRetries);
                    
                    await Task.Delay(delayMs, cancellationToken);
                }
            }
            
            // If we get here, we've exhausted all retries or models
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
            // First, determine the model to use
            var strategy = routingStrategy ?? _defaultRoutingStrategy;
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
                
                // Set the selected model in the request
                request.Model = GetModelAliasForDeployment(modelToUse);
            }
            
            // Now stream from the selected model
            _logger.LogInformation("Streaming from model {ModelName}", modelToUse);
            
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
                UpdateModelHealth(modelToUse, false);
                _logger.LogError(ex, "Error creating stream from model {ModelName}", modelToUse);
                throw;
            }
            
            // Now iterate through the stream
            bool receivedAnyChunks = false;
            
            // We can't use try-catch here, so we'll handle errors at a higher level
            await foreach (var chunk in stream)
            {
                receivedAnyChunks = true;
                yield return chunk;
            }
            
            stopwatch.Stop();
            
            // After streaming completes, update model statistics
            if (receivedAnyChunks)
            {
                // Success
                UpdateModelHealth(modelToUse, true);
                IncrementModelUsage(modelToUse);
                UpdateModelLatency(modelToUse, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                // No chunks received - update health status
                UpdateModelHealth(modelToUse, false);
                throw new LLMCommunicationException($"No chunks received from model {modelToUse}");
            }
        }

        /// <summary>
        /// Select a model for streaming, handling retries and fallbacks
        /// </summary>
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
                
                // Select a model based on strategy
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
        /// Selects a model based on the specified strategy
        /// </summary>
        private async Task<string?> SelectModelAsync(
            string? requestedModel, 
            string strategy, 
            List<string> excludeModels,
            CancellationToken cancellationToken)
        {
            // Small delay to make this actually async
            await Task.Delay(1, cancellationToken);
            
            // Create a list of candidate models based on the requested model and strategy
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
            
            // Filter to only healthy models
            var availableModels = candidateModels
                .Where(m => !_modelHealthStatus.TryGetValue(m, out var healthy) || healthy)
                .ToList();
            
            if (!availableModels.Any())
            {
                return null;
            }
            
            // Get deployments for the available models
            var availableDeployments = availableModels
                .Select(m => _modelDeployments.TryGetValue(m, out var deployment) ? deployment : null)
                .Where(d => d != null)
                .ToList();
                
            // Parse strategy string to enum if possible
            if (Enum.TryParse<RoutingStrategy>(strategy, true, out var routingStrategyEnum))
            {
                return routingStrategyEnum switch
                {
                    RoutingStrategy.Simple => availableModels.FirstOrDefault(),
                    
                    RoutingStrategy.RoundRobin => SelectRoundRobin(availableModels),
                    
                    RoutingStrategy.LeastCost => availableDeployments.Any() ? 
                        availableDeployments
                            .OrderBy(d => d!.InputTokenCostPer1K ?? decimal.MaxValue)
                            .ThenBy(d => d!.OutputTokenCostPer1K ?? decimal.MaxValue)
                            .Select(d => d!.DeploymentName)
                            .FirstOrDefault() : 
                        availableModels.FirstOrDefault(),
                    
                    RoutingStrategy.LeastLatency => availableDeployments.Any() ?
                        availableDeployments
                            .OrderBy(d => d!.AverageLatencyMs)
                            .Select(d => d!.DeploymentName)
                            .FirstOrDefault() :
                        availableModels.FirstOrDefault(),
                        
                    RoutingStrategy.HighestPriority => availableDeployments.Any() ?
                        availableDeployments
                            .OrderBy(d => d!.Priority)
                            .Select(d => d!.DeploymentName)
                            .FirstOrDefault() :
                        availableModels.FirstOrDefault(),
                        
                    _ => availableModels.FirstOrDefault()
                };
            }
            
            // Fall back to string-based strategies for backward compatibility
            return strategy.ToLowerInvariant() switch
            {
                "simple" => availableModels.FirstOrDefault(),
                
                "random" => availableModels[_random.Next(availableModels.Count)],
                
                "roundrobin" => SelectRoundRobin(availableModels),
                
                "leastused" => availableModels
                    .OrderBy(m => _modelUsageCount.TryGetValue(m, out var count) ? count : 0)
                    .FirstOrDefault(),
                
                _ => availableModels.FirstOrDefault() // Default to simple strategy
            };
        }

        /// <summary>
        /// Selects a model using round-robin strategy
        /// </summary>
        private string SelectRoundRobin(List<string> availableModels)
        {
            // Simple round-robin by selecting the least recently used model
            return availableModels
                .OrderBy(m => _modelUsageCount.TryGetValue(m, out var count) ? count : 0)
                .First();
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
        private bool IsRecoverableError(Exception ex)
        {
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
