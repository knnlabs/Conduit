using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using ConduitLLM.Configuration.Interfaces;
namespace ConduitLLM.Core
{
    /// <summary>
    /// Main entry point for interacting with the ConduitLLM library.
    /// Orchestrates calls to different LLM providers based on configuration via an <see cref="ILLMClientFactory"/>.
    /// </summary>
    public class Conduit : IConduit
    {
        private readonly ILLMClientFactory _clientFactory;
        private readonly IContextManager? _contextManager;
        private readonly IModelProviderMappingService? _modelProviderMappingService;
        private readonly IOptions<ContextManagementOptions>? _contextOptions;
        private readonly ILogger<Conduit> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="Conduit"/> class.
        /// </summary>
        /// <param name="clientFactory">The factory used to obtain provider-specific LLM clients.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="contextManager">Optional context manager for handling token limits.</param>
        /// <param name="modelProviderMappingService">Optional service to retrieve model mappings.</param>
        /// <param name="contextOptions">Optional configuration for context management.</param>
        /// <exception cref="ArgumentNullException">Thrown if clientFactory is null.</exception>
        public Conduit(
            ILLMClientFactory clientFactory,
            ILogger<Conduit> logger,
            IContextManager? contextManager = null,
            IModelProviderMappingService? modelProviderMappingService = null,
            IOptions<ContextManagementOptions>? contextOptions = null)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

            // Get the appropriate client from the factory based on the model alias in the request
            ILLMClient client = _clientFactory.GetClient(request.Model);

            // Call the client's method, passing the optional apiKey
            // Exceptions specific to providers (like communication errors) are expected to bubble up from the client.
            // The factory handles ConfigurationException and UnsupportedProviderException.
            return await client.CreateChatCompletionAsync(request, apiKey, cancellationToken).ConfigureAwait(false);
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

            // Get the appropriate client from the factory based on the model alias in the request
            ILLMClient client = _clientFactory.GetClient(request.Model);

            // Call the client's streaming method, passing the optional apiKey
            // Exceptions specific to providers (like communication errors) are expected to bubble up from the client.
            // The factory handles ConfigurationException and UnsupportedProviderException.
            await foreach (var chunk in client.StreamChatCompletionAsync(request, apiKey, cancellationToken))
            {
                yield return chunk;
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
                // Get model context window limit (MaxInputTokens) from database entities only
                int? maxInputTokens = null;

                // Try to get model-specific context limit from database
                var mapping = await _modelProviderMappingService.GetMappingByModelAliasAsync(request.Model);

                if (mapping != null)
                {
                    // Check for provider-specific override first (ModelProviderTypeAssociation.MaxInputTokens)
                    if (mapping.ModelProviderTypeAssociation?.MaxInputTokens.HasValue == true)
                    {
                        maxInputTokens = mapping.ModelProviderTypeAssociation.MaxInputTokens.Value;
                        _logger.LogDebug("Using provider-specific MaxInputTokens of {Tokens} tokens for {Model} from {Provider}",
                            maxInputTokens, request.Model, mapping.Provider?.ProviderName ?? "Unknown");
                    }
                    // Fall back to base model's MaxInputTokens
                    else if (mapping.ModelProviderTypeAssociation?.Model?.MaxInputTokens.HasValue == true)
                    {
                        maxInputTokens = mapping.ModelProviderTypeAssociation.Model.MaxInputTokens.Value;
                        _logger.LogDebug("Using model's MaxInputTokens of {Tokens} tokens for {Model}",
                            maxInputTokens, request.Model);
                    }
                    else
                    {
                        _logger.LogDebug("No MaxInputTokens configured for {Model} - context management will not be applied",
                            request.Model);
                    }
                }
                else
                {
                    _logger.LogWarning("No model mapping found for {Model} - context management cannot be applied",
                        request.Model);
                }

                // Apply context management only if we have a limit from the database
                if (maxInputTokens.HasValue && _contextManager != null)
                {
                    return await _contextManager.ManageContextAsync(request, maxInputTokens.Value);
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
        /// Gets an LLM client for the specified model.
        /// </summary>
        /// <param name="modelAlias">The model alias to get a client for.</param>
        /// <returns>The LLM client for the specified model.</returns>
        public ILLMClient GetClient(string modelAlias)
        {
            return _clientFactory.GetClient(modelAlias);
        }

        // Add other high-level methods as needed.
    }
}
