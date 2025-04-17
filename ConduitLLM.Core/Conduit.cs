using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces; // Use original location for ILLMClient and ILLMClientFactory
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core;

/// <summary>
/// Main entry point for interacting with the ConduitLLM library.
/// Orchestrates calls to different LLM providers based on configuration via an <see cref="ILLMClientFactory"/>.
/// </summary>
public class Conduit // Consider making this non-static and using dependency injection
{
    private readonly ILLMClientFactory _clientFactory;
    private readonly ILLMRouter? _router;

    /// <summary>
    /// Initializes a new instance of the <see cref="Conduit"/> class.
    /// </summary>
    /// <param name="clientFactory">The factory used to obtain provider-specific LLM clients.</param>
    /// <param name="router">Optional router for load balancing and fallback (if null, direct model calls will be used).</param>
    /// <exception cref="ArgumentNullException">Thrown if clientFactory is null.</exception>
    public Conduit(ILLMClientFactory clientFactory, ILLMRouter? router = null)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _router = router;
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
    public IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
        ChatCompletionRequest request,
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Model))
        {
            throw new ArgumentException("The request must specify a target Model alias.", "request.Model");
        }

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
            return _router.StreamChatCompletionAsync(request, routingStrategy, apiKey, cancellationToken);
        }
        else
        {
            // Use direct model access via client factory (original behavior)
            // 1. Get the appropriate client from the factory based on the model alias in the request
            ILLMClient client = _clientFactory.GetClient(request.Model);

            // 2. Call the client's streaming method, passing the optional apiKey
            // Exceptions specific to providers (like communication errors) are expected to bubble up from the client.
            // The factory handles ConfigurationException and UnsupportedProviderException.
            return client.StreamChatCompletionAsync(request, apiKey, cancellationToken);
        }
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

    // Add other high-level methods as needed (e.g., GetEmbeddingsAsync).
    // public Task<EmbeddingsResponse> GetEmbeddingsAsync(EmbeddingsRequest request, CancellationToken cancellationToken = default)
    // {
    //     // Similar logic: get client, call client method
    //     throw new NotImplementedException();
    // }
}
