using System.Diagnostics;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Routing;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Routing
{
    /// <summary>
    /// Embedding functionality for the DefaultLLMRouter.
    /// </summary>
    public partial class DefaultLLMRouter
    {
        /// <inheritdoc/>
        public async Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
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

            _logger.LogDebug("Processing embedding request using {Strategy} strategy", strategy);

            // Check for passthrough mode first
            if (ShouldUsePassthroughModeForEmbedding(request, strategy))
            {
                _logger.LogDebug("Using passthrough mode for embedding model {Model}", request.Model);
                return await DirectEmbeddingPassthroughAsync(request, apiKey, cancellationToken);
            }

            // Otherwise use normal routing with retries
            return await RouteEmbeddingThroughLoadBalancerAsync(request, originalModelRequested, strategy, apiKey, cancellationToken);
        }

        /// <summary>
        /// Determines if an embedding request should be handled in passthrough mode.
        /// </summary>
        /// <param name="request">The embedding request.</param>
        /// <param name="strategy">The routing strategy.</param>
        /// <returns>True if the request should be handled in passthrough mode, false otherwise.</returns>
        private bool ShouldUsePassthroughModeForEmbedding(EmbeddingRequest request, string strategy)
        {
            return !string.IsNullOrEmpty(request.Model) &&
                   strategy.Equals("passthrough", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Directly passes the embedding request to the specified model without routing.
        /// </summary>
        /// <param name="request">The embedding request.</param>
        /// <param name="apiKey">Optional API key to use for the request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The embedding response.</returns>
        private async Task<EmbeddingResponse> DirectEmbeddingPassthroughAsync(
            EmbeddingRequest request,
            string? apiKey,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(request.Model))
            {
                throw new ValidationException("Model must be specified for embedding requests in passthrough mode");
            }

            try
            {
                var client = _clientFactory.GetClient(request.Model);
                return await client.CreateEmbeddingAsync(request, apiKey, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during embedding pass-through to model {Model}", request.Model);
                throw;
            }
        }

        /// <summary>
        /// Routes an embedding request through the load balancer with retry logic.
        /// </summary>
        /// <param name="request">The embedding request.</param>
        /// <param name="originalModel">The original model requested.</param>
        /// <param name="strategy">The routing strategy to use.</param>
        /// <param name="apiKey">Optional API key to use for the request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The embedding response.</returns>
        /// <exception cref="LLMCommunicationException">Thrown when all attempts fail due to communication errors.</exception>
        /// <exception cref="ModelUnavailableException">Thrown when no suitable model is available.</exception>
        private async Task<EmbeddingResponse> RouteEmbeddingThroughLoadBalancerAsync(
            EmbeddingRequest request,
            string? originalModel,
            string strategy,
            string? apiKey,
            CancellationToken cancellationToken)
        {
            List<string> attemptedModels = new();
            var attemptContext = new AttemptContext();

            // Attempt to execute the embedding request with retries
            var result = await ExecuteEmbeddingWithRetriesAsync(
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
            HandleFailedEmbeddingAttempts(attemptContext.LastException, originalModel, attemptedModels, attemptContext.AttemptCount);

            // This line will never be reached, but is required for compilation
            throw new ModelUnavailableException(
                $"No suitable embedding model found for {originalModel} after {attemptContext.AttemptCount} attempts");
        }

        /// <summary>
        /// Executes an embedding request with retry logic and fallback handling.
        /// </summary>
        /// <param name="request">The embedding request.</param>
        /// <param name="originalModelRequested">The original model name requested.</param>
        /// <param name="strategy">The routing strategy to use.</param>
        /// <param name="attemptedModels">List of models that have already been attempted.</param>
        /// <param name="attemptContext">Context object holding attempt count and exception details.</param>
        /// <param name="apiKey">Optional API key to use for the request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The embedding response if successful, null otherwise.</returns>
        private async Task<EmbeddingResponse?> ExecuteEmbeddingWithRetriesAsync(
            EmbeddingRequest request,
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

                // Attempt the embedding request execution with a specific model
                var result = await TryEmbeddingRequestExecutionWithSelectedModelAsync(
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
        /// Attempts to execute an embedding request with a selected model.
        /// </summary>
        private async Task<EmbeddingResponse?> TryEmbeddingRequestExecutionWithSelectedModelAsync(
            EmbeddingRequest request,
            string? originalModelRequested,
            string strategy,
            List<string> attemptedModels,
            AttemptContext attemptContext,
            string? apiKey,
            CancellationToken cancellationToken)
        {
            // Get the next model based on strategy, filtering for embedding-capable models
            string? selectedModel = await SelectEmbeddingModelAsync(
                originalModelRequested,
                strategy,
                attemptedModels,
                cancellationToken);

            if (selectedModel == null)
            {
                _logger.LogWarning("No suitable embedding model found");
                attemptContext.LastException = new ModelUnavailableException(
                    "No suitable embedding model is available to process this request");
                return null;
            }

            _logger.LogInformation("Selected embedding model {ModelName} for request using {Strategy} strategy",
                selectedModel, strategy);

            // Add this model to the list of attempted ones
            attemptedModels.Add(selectedModel);

            // Try to execute with the selected model
            return await TryExecuteEmbeddingRequestAsync(
                request,
                selectedModel,
                attemptContext,
                apiKey,
                cancellationToken);
        }

        /// <summary>
        /// Selects an appropriate model for embedding requests based on the routing strategy.
        /// </summary>
        /// <param name="requestedModel">The model name originally requested by the client.</param>
        /// <param name="strategy">The routing strategy to use for selection.</param>
        /// <param name="excludeModels">List of model names to exclude from consideration.</param>
        /// <param name="cancellationToken">A token for cancelling the operation.</param>
        /// <returns>The name of the selected model, or null if no suitable model could be found.</returns>
        private async Task<string?> SelectEmbeddingModelAsync(
            string? requestedModel,
            string strategy,
            List<string> excludeModels,
            CancellationToken cancellationToken)
        {
            // Small delay to make this actually async
            await Task.Delay(1, cancellationToken);

            // Get filtered list of available models that support embeddings
            var (availableModels, availableDeployments) = await GetFilteredEmbeddingModelsAsync(
                requestedModel, excludeModels, cancellationToken);

            if (availableModels.Count() == 0)
            {
                _logger.LogWarning("No available embedding models found for requestedModel={RequestedModel}", requestedModel);
                return null;
            }

            // Handle passthrough strategy as a special case
            if (IsPassthroughStrategy(strategy))
            {
                return availableModels.FirstOrDefault();
            }

            // Select model using the appropriate strategy
            return SelectModelUsingStrategy(strategy, availableModels, availableDeployments, false);
        }

        /// <summary>
        /// Gets a filtered list of available models that support embeddings.
        /// </summary>
        private async Task<(List<string> AvailableModels, Dictionary<string, ModelDeployment> AvailableDeployments)>
            GetFilteredEmbeddingModelsAsync(string? requestedModel, List<string> excludeModels, CancellationToken cancellationToken)
        {
            // Add small delay to ensure method is truly async
            await Task.Delay(1, cancellationToken);

            // Build candidate models list (same as regular routing)
            var candidateModels = BuildCandidateModelsList(requestedModel, excludeModels);

            // Filter to only models that support embeddings (checking deployment SupportsEmbeddings or model capabilities)
            var embeddingCapableModels = FilterEmbeddingCapableModels(candidateModels);

            // Filter to only healthy models
            var availableModels = FilterHealthyModels(embeddingCapableModels);

            // Get deployment information for available models
            var availableDeployments = GetAvailableDeployments(availableModels);

            return (availableModels, availableDeployments);
        }

        /// <summary>
        /// Filters a list of candidate models to include only those that support embeddings.
        /// </summary>
        /// <param name="candidateModels">The list of candidate model names.</param>
        /// <returns>A filtered list containing only embedding-capable models.</returns>
        private List<string> FilterEmbeddingCapableModels(List<string> candidateModels)
        {
            return candidateModels
                .Where(m => 
                {
                    // Check if the deployment supports embeddings
                    if (_modelDeployments.TryGetValue(m, out var deployment))
                    {
                        // Use the SupportsEmbeddings property to determine capability
                        return deployment.SupportsEmbeddings;
                    }
                    
                    // If no deployment info, check if the model name suggests embedding capability
                    var modelLower = m.ToLower();
                    return modelLower.Contains("embed") || modelLower.Contains("ada") || 
                           modelLower.Contains("text-embedding") || modelLower.Contains("e5");
                })
                .ToList();
        }

        /// <summary>
        /// Attempts to execute an embedding request with a specific model and tracks any exceptions.
        /// </summary>
        /// <param name="request">The embedding request.</param>
        /// <param name="selectedModel">The model to use for the request.</param>
        /// <param name="attemptContext">Context object to track attempts and capture exceptions.</param>
        /// <param name="apiKey">Optional API key to use for the request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The embedding response if successful, null otherwise.</returns>
        private async Task<EmbeddingResponse?> TryExecuteEmbeddingRequestAsync(
            EmbeddingRequest request,
            string selectedModel,
            AttemptContext attemptContext,
            string? apiKey,
            CancellationToken cancellationToken)
        {
            // Apply the selected model
            request.Model = GetModelAliasForDeployment(selectedModel);

            try
            {
                return await ExecuteEmbeddingModelRequestAsync(request, selectedModel, apiKey, cancellationToken);
            }
            catch (Exception ex)
            {
                HandleEmbeddingExecutionException(ex, selectedModel);
                attemptContext.LastException = ex;
                return null;
            }
        }

        /// <summary>
        /// Executes an embedding request with the specified model and tracks metrics.
        /// </summary>
        /// <param name="request">The embedding request with model set.</param>
        /// <param name="selectedModel">The model to use for tracking metrics.</param>
        /// <param name="apiKey">Optional API key to use for the request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The embedding response.</returns>
        private async Task<EmbeddingResponse> ExecuteEmbeddingModelRequestAsync(
            EmbeddingRequest request,
            string selectedModel,
            string? apiKey,
            CancellationToken cancellationToken)
        {
            // Check cache first if available
            string? cacheKey = null;
            if (_embeddingCache?.IsAvailable == true)
            {
                cacheKey = _embeddingCache.GenerateCacheKey(request);
                var cachedResponse = await _embeddingCache.GetEmbeddingAsync(cacheKey);
                if (cachedResponse != null)
                {
                    _logger.LogDebug("Cache hit for embedding request with model {Model}", selectedModel);
                    // Still update model stats for cache hits to track usage
                    UpdateModelStatistics(selectedModel, 0); // 0ms latency for cache hits
                    return cachedResponse;
                }
            }

            // Track execution time for metrics
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                // Get the client for this model and execute the request
                var client = _clientFactory.GetClient(request.Model!);
                var result = await client.CreateEmbeddingAsync(request, apiKey, cancellationToken);

                stopwatch.Stop();

                // Update model stats on success
                UpdateModelStatistics(selectedModel, stopwatch.ElapsedMilliseconds);

                // Cache the result if caching is available
                if (_embeddingCache?.IsAvailable == true && cacheKey != null)
                {
                    try
                    {
                        await _embeddingCache.SetEmbeddingAsync(cacheKey, result);
                        _logger.LogDebug("Cached embedding response for model {Model}", selectedModel);
                    }
                    catch (Exception cacheEx)
                    {
                        _logger.LogWarning(cacheEx, "Failed to cache embedding response for model {Model}", selectedModel);
                    }
                }

                return result;
            }
            catch (Exception)
            {
                stopwatch.Stop();
                throw; // Re-throw to be handled by the caller
            }
        }

        /// <summary>
        /// Handles exceptions that occur during embedding request execution.
        /// </summary>
        /// <param name="exception">The exception that occurred.</param>
        /// <param name="selectedModel">The model that was used.</param>
        private void HandleEmbeddingExecutionException(Exception exception, string selectedModel)
        {
            _logger.LogWarning(exception, "Embedding request to model {ModelName} failed",
                selectedModel);
        }

        /// <summary>
        /// Handles the case where all attempts to execute an embedding request have failed.
        /// </summary>
        /// <param name="lastException">The last exception that occurred.</param>
        /// <param name="originalModelRequested">The original model name requested.</param>
        /// <param name="attemptedModels">List of models that were attempted.</param>
        /// <param name="attemptCount">The number of attempts that were made.</param>
        private void HandleFailedEmbeddingAttempts(
            Exception? lastException,
            string? originalModelRequested,
            List<string> attemptedModels,
            int attemptCount)
        {
            if (lastException != null)
            {
                _logger.LogError(lastException,
                    "All embedding attempts failed for model {OriginalModel} after trying {ModelCount} models with {AttemptCount} attempts",
                    originalModelRequested, attemptedModels.Count(), attemptCount);

                throw new LLMCommunicationException(
                    $"Failed to process embedding request after {attemptCount} attempts across {attemptedModels.Count()} models",
                    lastException);
            }

            throw new ModelUnavailableException(
                $"No suitable embedding model found for {originalModelRequested} after {attemptCount} attempts");
        }
    }
}