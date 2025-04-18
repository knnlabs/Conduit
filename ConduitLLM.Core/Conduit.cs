using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Configuration;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core
{
    /// <summary>
    /// Main entry point for interacting with the ConduitLLM library.
    /// Orchestrates calls to different LLM providers based on configuration via an <see cref="ILLMClientFactory"/>.
    /// </summary>
    public class Conduit
    {
        private readonly ILLMClientFactory _clientFactory;
        private readonly ILLMRouter? _router;
        private readonly IContextManager? _contextManager;
        private readonly IModelProviderMappingService? _modelProviderMappingService;
        private readonly IOptions<ContextManagementOptions>? _contextOptions;
        private readonly ILogger<Conduit> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="Conduit"/> class.
        /// </summary>
        /// <param name="clientFactory">The factory used to obtain provider-specific LLM clients.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="router">Optional router for load balancing and fallback (if null, direct model calls will be used).</param>
        /// <param name="contextManager">Optional context manager for handling token limits.</param>
        /// <param name="modelProviderMappingService">Optional service to retrieve model mappings.</param>
        /// <param name="contextOptions">Optional configuration for context management.</param>
        /// <exception cref="ArgumentNullException">Thrown if clientFactory is null.</exception>
        public Conduit(
            ILLMClientFactory clientFactory,
            ILogger<Conduit> logger,
            ILLMRouter? router = null,
            IContextManager? contextManager = null,
            IModelProviderMappingService? modelProviderMappingService = null,
            IOptions<ContextManagementOptions>? contextOptions = null)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _router = router;
            _contextManager = contextManager;
            _modelProviderMappingService = modelProviderMappingService;
            _contextOptions = contextOptions;
        }

        /// <summary>
        /// Creates a chat completion using the configured LLM providers.
        /// </summary>
        /// <param name="request">The chat completion request, including the target model alias.</param>
        /// <param name="apiKey">Optional API key to override the configured key for this request.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The chat completion response from the selected LLM provider.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the request is null.</exception>
        /// <exception cref="ArgumentException">Thrown if the request.Model is null or whitespace.</exception>
        /// <exception cref="ConfigurationException">Thrown if configuration for the requested model is invalid or missing.</exception>
        /// <exception cref="UnsupportedProviderException">Thrown if the provider for the requested model is not supported.</exception>
        /// <exception cref="LLMCommunicationException">Thrown if communication with the LLM provider fails.</exception>
        public async Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(request.Model))
            {
                throw new ArgumentException("The request must specify a target Model alias.", "request.Model");
            }

            // Apply context management if enabled
            request = await ApplyContextManagementAsync(request);

            // If a router is configured and the model uses the 'router:' prefix, use the router
            if (_router != null && IsRouterRequest(request.Model))
            {
                // Extract the routing strategy if specified in the model name
                var (routingStrategy, actualModel) = ExtractRoutingInfoFromModel(request.Model);
                
                // Set the cleaned model name back in the request if provided
                if (!string.IsNullOrEmpty(actualModel))
                {
                    request.Model = actualModel;
                }

                // Use the router for this request
                return await _router.CreateChatCompletionAsync(request, routingStrategy, apiKey, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                // Use direct model access via client factory (original behavior)
                // 1. Get the appropriate client from the factory based on the model alias in the request
                ILLMClient client = _clientFactory.GetClient(request.Model);

                // 2. Call the client's method, passing the optional apiKey
                // Exceptions specific to providers (like communication errors) are expected to bubble up from the client.
                // The factory handles ConfigurationException and UnsupportedProviderException.
                return await client.CreateChatCompletionAsync(request, apiKey, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Creates a streaming chat completion using the configured LLM providers.
        /// </summary>
        /// <param name="request">The chat completion request, including the target model alias.</param>
        /// <param name="apiKey">Optional API key to override the configured key for this request.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>An asynchronous enumerable of chat completion chunks from the selected LLM provider.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the request is null.</exception>
        /// <exception cref="ArgumentException">Thrown if the request.Model is null or whitespace.</exception>
        /// <exception cref="ConfigurationException">Thrown if configuration for the requested model is invalid or missing.</exception>
        /// <exception cref="UnsupportedProviderException">Thrown if the provider for the requested model is not supported.</exception>
        /// <exception cref="LLMCommunicationException">Thrown if communication with the LLM provider fails during streaming.</exception>
        public async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(request.Model))
            {
                throw new ArgumentException("The request must specify a target Model alias.", "request.Model");
            }

            // Apply context management if enabled
            request = await ApplyContextManagementAsync(request);

            // If a router is configured and the model uses the 'router:' prefix, use the router
            if (_router != null && IsRouterRequest(request.Model))
            {
                // Extract the routing strategy if specified in the model name
                var (routingStrategy, actualModel) = ExtractRoutingInfoFromModel(request.Model);
                
                // Set the cleaned model name back in the request if provided
                if (!string.IsNullOrEmpty(actualModel))
                {
                    request.Model = actualModel;
                }

                // Use the router for this streaming request
                await foreach (var chunk in _router.StreamChatCompletionAsync(request, routingStrategy, apiKey, cancellationToken))
                {
                    yield return chunk;
                }
            }
            else
            {
                // Use direct model access via client factory (original behavior)
                // 1. Get the appropriate client from the factory based on the model alias in the request
                ILLMClient client = _clientFactory.GetClient(request.Model);

                // 2. Call the client's streaming method, passing the optional apiKey
                // Exceptions specific to providers (like communication errors) are expected to bubble up from the client.
                // The factory handles ConfigurationException and UnsupportedProviderException.
                await foreach (var chunk in client.StreamChatCompletionAsync(request, apiKey, cancellationToken))
                {
                    yield return chunk;
                }
            }
        }

        /// <summary>
        /// Applies context window management to trim message history if needed.
        /// </summary>
        /// <param name="request">The original chat completion request</param>
        /// <returns>The request with potentially trimmed messages</returns>
        private async Task<ChatCompletionRequest> ApplyContextManagementAsync(ChatCompletionRequest request)
        {
            // Skip if context management is disabled or services aren't available
            if (_contextManager == null || _modelProviderMappingService == null || _contextOptions == null || 
                !_contextOptions.Value.EnableAutomaticContextManagement)
            {
                return request;
            }

            try
            {
                // Get model context window limit
                int? maxContextTokens = null;
                
                // First try to get model-specific context limit
                var mapping = await _modelProviderMappingService.GetMappingByModelAliasAsync(request.Model);
                
                // Handle the MaxContextTokens property which may or may not exist yet
                // depending on whether the migration has been applied
                var maxContextTokensProperty = mapping?.GetType().GetProperty("MaxContextTokens");
                if (mapping != null && maxContextTokensProperty != null)
                {
                    maxContextTokens = maxContextTokensProperty.GetValue(mapping) as int?;
                    if (maxContextTokens.HasValue)
                    {
                        _logger.LogDebug("Using model-specific context window limit of {Tokens} tokens for {Model}", 
                            maxContextTokens, request.Model);
                    }
                }
                
                // Fall back to default limit if configured
                if (!maxContextTokens.HasValue && _contextOptions.Value.DefaultMaxContextTokens.HasValue)
                {
                    maxContextTokens = _contextOptions.Value.DefaultMaxContextTokens;
                    _logger.LogDebug("Using default context window limit of {Tokens} tokens for {Model}", 
                        maxContextTokens, request.Model);
                }
                
                // Apply context management if we have a limit
                if (maxContextTokens.HasValue && _contextManager != null)
                {
                    return await _contextManager.ManageContextAsync(request, maxContextTokens.Value);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't fail the request - just pass through without context management
                _logger.LogError(ex, "Error applying context management for model {Model}", request.Model);
            }
            
            return request;
        }

        /// <summary>
        /// Creates an embedding using the configured LLM providers.
        /// </summary>
        /// <param name="request">The embedding request, including the target model alias.</param>
        /// <param name="apiKey">Optional API key to override the configured key for this request.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The embedding response from the selected LLM provider.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the request is null.</exception>
        /// <exception cref="ArgumentException">Thrown if the request.Model is null or whitespace.</exception>
        /// <exception cref="ConfigurationException">Thrown if configuration for the requested model is invalid or missing.</exception>
        /// <exception cref="UnsupportedProviderException">Thrown if the provider for the requested model is not supported.</exception>
        /// <exception cref="LLMCommunicationException">Thrown if communication with the LLM provider fails.</exception>
        public async Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(request.Model))
                throw new ArgumentException("The request must specify a target Model alias.", "request.Model");

            // No router for embeddings (OpenAI spec does not support routing for embeddings)
            ILLMClient client = _clientFactory.GetClient(request.Model);
            return await client.CreateEmbeddingAsync(request, apiKey, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates an image generation using the configured LLM providers.
        /// </summary>
        /// <param name="request">The image generation request, including the target model alias.</param>
        /// <param name="apiKey">Optional API key to override the configured key for this request.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The image generation response from the selected LLM provider.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the request is null.</exception>
        /// <exception cref="ArgumentException">Thrown if the request.Model is null or whitespace.</exception>
        /// <exception cref="ConfigurationException">Thrown if configuration for the requested model is invalid or missing.</exception>
        /// <exception cref="UnsupportedProviderException">Thrown if the provider for the requested model is not supported.</exception>
        /// <exception cref="LLMCommunicationException">Thrown if communication with the LLM provider fails.</exception>
        public async Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(request.Model))
                throw new ArgumentException("The request must specify a target Model alias.", "request.Model");

            // No router for image generation (OpenAI spec does not support routing for images)
            ILLMClient client = _clientFactory.GetClient(request.Model);
            return await client.CreateImageAsync(request, apiKey, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the router instance if one is configured
        /// </summary>
        /// <returns>The router instance or null if none is configured</returns>
        public ILLMRouter? GetRouter() => _router;

        /// <summary>
        /// Determines if a model request should be handled by the router
        /// </summary>
        /// <param name="modelName">The model name to check</param>
        /// <returns>True if this is a router request, false otherwise</returns>
        private bool IsRouterRequest(string modelName)
        {
            return modelName.StartsWith("router:", StringComparison.OrdinalIgnoreCase) ||
                   modelName.Equals("router", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Extracts routing information from a model name
        /// </summary>
        /// <param name="modelName">The model name to parse</param>
        /// <returns>Tuple containing routing strategy and actual model name (both may be null)</returns>
        private (string? strategy, string? model) ExtractRoutingInfoFromModel(string modelName)
        {
            // Default case: just "router"
            if (modelName.Equals("router", StringComparison.OrdinalIgnoreCase))
            {
                return (null, null);
            }

            // Model name format: router:strategy:model or router:strategy or router:model
            if (modelName.StartsWith("router:", StringComparison.OrdinalIgnoreCase))
            {
                string remaining = modelName.Substring("router:".Length);
                
                // Split by colon to extract strategy and model (if present)
                var parts = remaining.Split(':', 2);
                
                if (parts.Length == 2)
                {
                    // Format: router:strategy:model
                    return (parts[0], parts[1]);
                }
                else
                {
                    // Could be either router:strategy or router:model
                    // Check if the remaining part is a known strategy
                    if (IsKnownStrategy(parts[0]))
                    {
                        return (parts[0], null);
                    }
                    else
                    {
                        // Assume it's a model name
                        return (null, parts[0]);
                    }
                }
            }
            
            // Not a router format
            return (null, modelName);
        }
        
        /// <summary>
        /// Checks if a string is a known routing strategy
        /// </summary>
        private bool IsKnownStrategy(string strategy)
        {
            // List of supported strategies
            return new[] { "simple", "random", "roundrobin", "leastused", "passthrough" }
                .Contains(strategy.ToLowerInvariant());
        }

        // Add other high-level methods as needed.
    }
}
