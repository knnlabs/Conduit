using System.Diagnostics;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Routing
{
    /// <summary>
    /// Chat completion functionality for the DefaultLLMRouter.
    /// </summary>
    public partial class DefaultLLMRouter
    {
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
            // Check if request contains images and requires vision capabilities
            bool containsImages = false;

            if (_capabilityDetector != null)
            {
                containsImages = _capabilityDetector.ContainsImageContent(request);
                if (containsImages)
                {
                    _logger.LogInformation("Request contains image content, selecting a vision-capable model");
                }
            }
            else
            {
                // Fallback check for images if capability detector isn't available
                foreach (var message in request.Messages)
                {
                    if (message.Content != null && message.Content is not string)
                    {
                        // Simple check for potential multimodal content - look for non-string content
                        containsImages = true; // If content is not a string, assume it might contain images
                        _logger.LogInformation("Request potentially contains non-text content (basic detection)");
                        break;
                    }
                }
            }

            // Get the next model based on strategy, considering vision requirements
            string? selectedModel = await SelectModelAsync(
                originalModelRequested,
                strategy,
                attemptedModels,
                cancellationToken,
                containsImages);

            if (selectedModel == null)
            {
                if (containsImages)
                {
                    _logger.LogWarning("No suitable vision-capable model found");
                    attemptContext.LastException = new ModelUnavailableException(
                        "No suitable vision-capable model is available to process this request with image content");
                }
                else
                {
                    _logger.LogWarning("No suitable model found");
                }
                return null;
            }

            _logger.LogInformation("Selected model {ModelName} for request using {Strategy} strategy{VisionCapable}",
                selectedModel, strategy, containsImages ? " (vision-capable)" : "");

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
    }
}