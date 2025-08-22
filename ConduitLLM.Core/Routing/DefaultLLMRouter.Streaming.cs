using System.Diagnostics;
using System.Runtime.CompilerServices;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Routing
{
    /// <summary>
    /// Streaming functionality for the DefaultLLMRouter.
    /// </summary>
    public partial class DefaultLLMRouter
    {
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

            // Check if request contains images and requires vision capabilities
            bool containsImages = false;
            if (_capabilityDetector != null)
            {
                containsImages = _capabilityDetector.ContainsImageContent(request);
                if (containsImages)
                {
                    _logger.LogInformation("Streaming request contains image content, selecting a vision-capable model");
                }
            }
            else
            {
                // Fallback check if capability detector is not available
                foreach (var message in request.Messages)
                {
                    if (message.Content != null && message.Content is not string)
                    {
                        containsImages = true; // If content is not a string, assume it might contain images
                        if (containsImages)
                        {
                            _logger.LogInformation("Streaming request potentially contains image content (basic detection)");
                            break;
                        }
                    }
                }
            }

            // If we're using a passthrough strategy and have a model, just use it directly
            if (!string.IsNullOrEmpty(request.Model) &&
                strategy.Equals("passthrough", StringComparison.OrdinalIgnoreCase))
            {
                modelToUse = request.Model;

                // Still need to check if the passthrough model supports vision if needed
                if (containsImages && _capabilityDetector != null &&
                    !_capabilityDetector.HasVisionCapability(modelToUse))
                {
                    throw new ModelUnavailableException(
                        $"Model {request.Model} does not support vision capabilities required by this streaming request");
                }
            }
            else
            {
                // Otherwise, select a model using our routing logic
                modelToUse = await SelectModelForStreamingAsync(
                    request.Model, strategy, _maxRetries, cancellationToken, containsImages);

                if (modelToUse == null)
                {
                    if (containsImages)
                    {
                        throw new ModelUnavailableException(
                            $"No suitable vision-capable model found for streaming request with original model {request.Model}");
                    }
                    else
                    {
                        throw new ModelUnavailableException(
                            $"No suitable model found for streaming request with original model {request.Model}");
                    }
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
                throw new LLMCommunicationException($"No chunks received from model {selectedModel}");
            }
        }

        /// <summary>
        /// Select a model for streaming, handling retries and fallbacks
        /// </summary>
        /// <param name="requestedModel">The model name originally requested by the client, or null if no specific model was requested.</param>
        /// <param name="strategy">The routing strategy to use for selection.</param>
        /// <param name="maxRetries">Maximum number of retry attempts.</param>
        /// <param name="cancellationToken">A token for cancelling the operation.</param>
        /// <param name="visionRequired">If true, only vision-capable models will be considered.</param>
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
            CancellationToken cancellationToken,
            bool visionRequired = false)
        {
            // Start tracking retry attempt count
            int attemptCount = 0;
            List<string> attemptedModels = new();

            while (attemptCount <= maxRetries)
            {
                attemptCount++;

                // Select a model based on strategy using the same SelectModelAsync method
                // that now delegates to our strategy pattern, with vision requirements if needed
                string? selectedModel = await SelectModelAsync(
                    requestedModel,
                    strategy,
                    attemptedModels,
                    cancellationToken,
                    visionRequired);

                if (selectedModel == null)
                {
                    string visionMessage = visionRequired ? " vision-capable" : "";
                    _logger.LogWarning("No suitable{VisionMessage} model found for streaming after {AttemptsCount} attempts",
                        visionMessage, attemptCount);
                    break;
                }

                _logger.LogInformation("Selected model {ModelName} for streaming using {Strategy} strategy{VisionMessage}",
                    selectedModel, strategy, visionRequired ? " (vision-capable)" : "");

                // Add this model to attempted list
                attemptedModels.Add(selectedModel);

                // Health checking removed - always use the selected model
                return selectedModel;
            }

            // No suitable model found
            return null;
        }
    }
}