using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Interfaces.Configuration;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Routing
{
    /// <summary>
    /// Default implementation of ILLMClientFactory that creates, manages, and caches LLM clients.
    /// </summary>
    /// <remarks>
    /// The DefaultLLMClientFactory handles the creation and caching of LLM client instances:
    /// 
    /// - Creates clients based on model aliases from mapping configuration
    /// - Caches clients to avoid unnecessary recreation
    /// - Handles provider-specific client creation through factory functions
    /// - Provides access to credentials and provider-specific configuration
    /// - Manages the lifecycle of LLM client instances
    /// 
    /// This factory enables the system to dynamically instantiate the appropriate client
    /// implementation for each LLM provider while abstracting the details of client
    /// construction from the rest of the application.
    /// </remarks>
    public class DefaultLLMClientFactory : ILLMClientFactory
    {
        private readonly ConduitRegistry _registry;
        private readonly Core.Interfaces.Configuration.IProviderCredentialService _credentialService;
        private readonly Core.Interfaces.Configuration.IModelProviderMappingService _mappingService;
        private readonly ILogger<DefaultLLMClientFactory> _logger;

        /// <summary>
        /// Cache of LLM clients indexed by model alias
        /// </summary>
        private readonly ConcurrentDictionary<string, ILLMClient> _clientCache = new(StringComparer.OrdinalIgnoreCase);

        
        /// <summary>
        /// Cache of LLM clients indexed by provider ID
        /// </summary>
        private readonly ConcurrentDictionary<int, ILLMClient> _providerIdClientCache = new();

        /// <summary>
        /// Maps provider names to factory functions that create client instances
        /// </summary>
        private readonly Dictionary<string, Func<string, ILLMClient>> _providerFactories = new(StringComparer.OrdinalIgnoreCase);
        
        /// <summary>
        /// Maps provider IDs to factory functions that create client instances
        /// </summary>
        private readonly Dictionary<int, Func<string, ILLMClient>> _providerIdFactories = new();

        /// <summary>
        /// Creates a new instance of DefaultLLMClientFactory
        /// </summary>
        /// <param name="registry">Conduit registry for model configuration</param>
        /// <param name="credentialService">Provider credential service</param>
        /// <param name="mappingService">Model mapping service</param>
        /// <param name="logger">Logger instance</param>
        public DefaultLLMClientFactory(
            ConduitRegistry registry,
            Core.Interfaces.Configuration.IProviderCredentialService credentialService,
            Core.Interfaces.Configuration.IModelProviderMappingService mappingService,
            ILogger<DefaultLLMClientFactory> logger)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _credentialService = credentialService ?? throw new ArgumentNullException(nameof(credentialService));
            _mappingService = mappingService ?? throw new ArgumentNullException(nameof(mappingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize provider factories (this would typically use factories from registered providers)
            InitializeProviderFactories();
        }

        /// <inheritdoc/>
        public ILLMClient GetClient(string modelAlias)
        {
            if (string.IsNullOrEmpty(modelAlias))
            {
                throw new ArgumentException("Model name cannot be null or empty", nameof(modelAlias));
            }

            // Try to get from cache first
            return _clientCache.GetOrAdd(modelAlias, CreateClient);
        }

        
        /// <inheritdoc/>
        public ILLMClient GetClientByProviderId(int providerId)
        {
            // Try to get from the provider ID cache first
            return _providerIdClientCache.GetOrAdd(providerId, CreateClientByProviderId);
        }

        /// <summary>
        /// Gets all available model aliases from the mapping service
        /// </summary>
        public IReadOnlyList<string> GetAvailableModels()
        {
            try
            {
                var mappings = _mappingService.GetAllMappingsAsync().GetAwaiter().GetResult();
                return mappings.Select(m => m.ModelAlias).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving available models");
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Creates a new LLM client for the specified model alias.
        /// </summary>
        /// <param name="modelAlias">The model alias to create a client for (e.g., "gpt-4", "claude-3").</param>
        /// <returns>An ILLMClient instance configured for the specified model.</returns>
        /// <exception cref="ConfigurationException">
        /// Thrown when no mapping is found for the model alias, when the provider is not supported,
        /// or when there's an error retrieving credentials or creating the client.
        /// </exception>
        /// <remarks>
        /// This method performs the following steps:
        /// 
        /// 1. Retrieves the model mapping for the given alias from the mapping service
        /// 2. Determines the appropriate provider name and model ID from the mapping
        /// 3. Looks up the factory function for the specified provider
        /// 4. Creates the client using the factory function with the provider-specific model ID
        /// 5. Returns a placeholder client if no factory exists for the provider
        /// 
        /// The created client is cached for future use, so subsequent requests for the
        /// same model alias will return the same client instance.
        /// </remarks>
        private ILLMClient CreateClient(string modelAlias)
        {
            try
            {
                // Get the mapping for this model alias
                var mapping = _mappingService.GetMappingByModelAliasAsync(modelAlias).GetAwaiter().GetResult();

                if (mapping == null)
                {
_logger.LogWarning("No mapping found for model alias {ModelAlias}", modelAlias.Replace(Environment.NewLine, ""));
                    throw new ConfigurationException($"No mapping found for model alias '{modelAlias}'");
                }

                // Get the provider ID and name from the mapping
                int providerId = ModelProviderMappingAdapter.GetProviderId(mapping);
                string providerName = ModelProviderMappingAdapter.GetProviderName(mapping);

_logger.LogInformation("Using provider ID {ProviderId} ({Provider}) for model {Model}", providerId, providerName.Replace(Environment.NewLine, ""), modelAlias.Replace(Environment.NewLine, ""));

                // Check if we have a factory for this provider ID first
                if (_providerIdFactories.TryGetValue(providerId, out var idFactory))
                {
                    return idFactory(ModelProviderMappingAdapter.GetProviderModelName(mapping));
                }
                
                // Fall back to provider name-based factory
                if (!_providerFactories.TryGetValue(providerName, out var factory))
                {
                    _logger.LogWarning("No factory found for provider ID {ProviderId} or name {ProviderName}", providerId, providerName);

                    // Return a placeholder client for demonstration purposes
                    return new PlaceholderLLMClient(ModelProviderMappingAdapter.GetProviderModelName(mapping), providerName, _logger);
                }

                // Create the client using the factory function
                return factory(ModelProviderMappingAdapter.GetProviderModelName(mapping));
            }
            catch (Exception ex) when (ex is not ConfigurationException)
            {
_logger.LogError(ex, "Error creating client for model {ModelAlias}".Replace(Environment.NewLine, ""), modelAlias.Replace(Environment.NewLine, ""));
                throw new ConfigurationException($"Error creating client for model '{modelAlias}'", ex);
            }
        }


        /// <summary>
        /// Creates a new LLM client for the specified provider ID
        /// </summary>
        private ILLMClient CreateClientByProviderId(int providerId)
        {
            try
            {
                // Get provider credentials
                var credentials = _credentialService.GetCredentialByIdAsync(providerId).GetAwaiter().GetResult()
                    ?? throw new ConfigurationException($"No credentials found for provider ID '{providerId}'");

                // Check if we have a factory for this provider ID
                if (!_providerIdFactories.TryGetValue(providerId, out var factory))
                {
                    _logger.LogWarning("No factory found for provider ID {ProviderId}", providerId);
                    
                    // Fall back to provider name-based factory if available
                    var providerName = ((ProviderType)providerId).ToString();
                    if (_providerFactories.TryGetValue(providerName, out var nameFactory))
                    {
                        return nameFactory(string.Empty);
                    }

                    // Return a placeholder client for demonstration purposes
                    return new PlaceholderLLMClient(null, providerName, _logger);
                }

                // Create the client using the factory function
                return factory(string.Empty);
            }
            catch (Exception ex) when (ex is not ConfigurationException)
            {
                _logger.LogError(ex, "Error creating client for provider ID {ProviderId}", providerId);
                throw new ConfigurationException($"Error creating client for provider ID '{providerId}'", ex);
            }
        }

        /// <summary>
        /// Initializes the provider factories
        /// </summary>
        private void InitializeProviderFactories()
        {
            // These would be registered by provider-specific extensions
            // For now, we'll just use a placeholder client for all providers
            _providerFactories["openai"] = modelId => new PlaceholderLLMClient(modelId, "openai", _logger);
            _providerFactories["anthropic"] = modelId => new PlaceholderLLMClient(modelId, "anthropic", _logger);
            _providerFactories["azureopenai"] = modelId => new PlaceholderLLMClient(modelId, "azureopenai", _logger);
            _providerFactories["cerebras"] = modelId => new PlaceholderLLMClient(modelId, "cerebras", _logger);
            
            // TODO: Initialize provider ID factories based on actual provider IDs from the database
            // This would typically be done during startup after loading provider configurations
        }
    }

    /// <summary>
    /// A placeholder LLM client implementation for testing, development, and fallback purposes.
    /// </summary>
    /// <remarks>
    /// The PlaceholderLLMClient provides a non-functional but structurally complete implementation 
    /// of the ILLMClient interface. It's used in the following scenarios:
    /// 
    /// - During development before real provider implementations are available
    /// - For testing the routing and factory infrastructure without real API calls
    /// - As a fallback when a requested provider doesn't have a registered factory
    /// - To provide meaningful error messages and logging in misconfiguration scenarios
    /// 
    /// The client returns predefined responses for chat completions and streaming,
    /// and throws NotSupportedException for embeddings and image generation.
    /// </remarks>
    internal class PlaceholderLLMClient : ILLMClient
    {
        private readonly string? _modelId;
        private readonly string _providerName;
        private readonly ILogger _logger;

        public PlaceholderLLMClient(string? modelId, string providerName, ILogger logger)
        {
            _modelId = modelId;
            _providerName = providerName ?? "unknown";
            _logger = logger;
        }

        public Task<ChatCompletionResponse> CreateChatCompletionAsync(ChatCompletionRequest request, string? apiKey = null, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Placeholder client received request for model {Model} with provider {Provider}",
                request.Model ?? _modelId, _providerName);

            return Task.FromResult(new ChatCompletionResponse
            {
                Id = Guid.NewGuid().ToString(),
                Model = request.Model ?? _modelId ?? "unknown-model",
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Choices = new List<Choice>
                {
                    new Choice
                    {
                        Index = 0,
                        Message = new Message
                        {
                            Role = "assistant",
                            Content = $"This is a placeholder response from {_providerName} provider."
                        },
                        FinishReason = "stop"
                    }
                },
                Usage = new Usage
                {
                    PromptTokens = 10,
                    CompletionTokens = 20,
                    TotalTokens = 30
                }
            });
        }

        public Task<EmbeddingResponse> CreateEmbeddingAsync(EmbeddingRequest request, string? apiKey = null, CancellationToken cancellationToken = default)
            => Task.FromException<EmbeddingResponse>(new NotSupportedException($"Embeddings are not supported for provider '{_providerName}'"));

        public Task<ImageGenerationResponse> CreateImageAsync(ImageGenerationRequest request, string? apiKey = null, CancellationToken cancellationToken = default)
            => Task.FromException<ImageGenerationResponse>(new NotSupportedException($"Image generation is not supported for provider '{_providerName}'"));

        public IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(ChatCompletionRequest request, string? apiKey = null, CancellationToken cancellationToken = default)
        {
            return StreamChatCompletionInternalAsync(request, cancellationToken);
        }

        private async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionInternalAsync(ChatCompletionRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var response = $"This is a placeholder streaming response from {_providerName} provider.";
            var words = response.Split(' ');
            var id = Guid.NewGuid().ToString();

            foreach (var word in words)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                yield return new ChatCompletionChunk
                {
                    Id = id,
                    Model = request.Model ?? _modelId ?? "unknown-model",
                    Object = "chat.completion.chunk",
                    Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Choices = new List<StreamingChoice>
                    {
                        new StreamingChoice
                        {
                            Index = 0,
                            Delta = new DeltaContent
                            {
                                Role = "assistant",
                                Content = word + " "
                            }
                        }
                    }
                };

                await Task.Delay(100, cancellationToken);
            }

            // Final chunk with finish reason
            yield return new ChatCompletionChunk
            {
                Id = id,
                Model = request.Model ?? _modelId ?? "unknown-model",
                Object = "chat.completion.chunk",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Choices = new List<StreamingChoice>
                {
                    new StreamingChoice
                    {
                        Index = 0,
                        Delta = new DeltaContent
                        {
                            Content = ""
                        },
                        FinishReason = "stop"
                    }
                }
            };
        }

        public Task<List<string>> ListModelsAsync(string? apiKey = null, CancellationToken cancellationToken = default)
        {
            // Return a simple list of placeholder model IDs for this provider
            var models = new List<string>
            {
                $"{_providerName}-model-1",
                $"{_providerName}-model-2"
            };

            return Task.FromResult(models);
        }

        public Task<ProviderCapabilities> GetCapabilitiesAsync(string? modelId = null)
        {
            // Return basic capabilities for placeholder - using default constructor
            var capabilities = new ProviderCapabilities();
            return Task.FromResult(capabilities);
        }
    }

    /// <summary>
    /// Adapter class to handle different ModelProviderMapping implementations and property access patterns.
    /// </summary>
    /// <remarks>
    /// This adapter provides a uniform interface for accessing properties from the ModelProviderMapping
    /// objects, abstracting away potential differences in implementation details or property names.
    /// It helps maintain compatibility between different versions of the mapping objects and
    /// shields the factory code from changes in the underlying data model.
    /// </remarks>
    internal static class ModelProviderMappingAdapter
    {
        /// <summary>
        /// Gets the provider ID from a model mapping.
        /// </summary>
        /// <param name="mapping">The model provider mapping object.</param>
        /// <returns>The ID of the provider.</returns>
        /// <remarks>
        /// The provider ID is a unique numeric identifier for the provider.
        /// This is more reliable than provider names which can contain typos.
        /// </remarks>
        public static int GetProviderId(Core.Interfaces.Configuration.ModelProviderMapping mapping)
        {
            // Get the provider ID from the mapping
            return mapping.ProviderId;
        }
        
        /// <summary>
        /// Gets the provider name from a model mapping.
        /// </summary>
        /// <param name="mapping">The model provider mapping object.</param>
        /// <returns>The name of the provider (e.g., "openai", "anthropic").</returns>
        /// <remarks>
        /// The provider name identifies which LLM service implementation should be used.
        /// This is used to look up the appropriate factory function and credentials.
        /// </remarks>
        public static string GetProviderName(Core.Interfaces.Configuration.ModelProviderMapping mapping)
        {
            // Get the provider name from the mapping's provider type
            return mapping.ProviderType.ToString();
        }

        /// <summary>
        /// Gets the provider-specific model identifier from a mapping.
        /// </summary>
        /// <param name="mapping">The model provider mapping object.</param>
        /// <returns>The provider-specific model ID (e.g., "gpt-4-turbo-preview" for OpenAI).</returns>
        /// <remarks>
        /// The provider model ID is the actual identifier that the provider's API expects,
        /// which may be different from the model alias used within the application.
        /// This method ensures we get the correct property even if the naming convention
        /// changes in the mapping object.
        /// </remarks>
        public static string GetProviderModelName(Core.Interfaces.Configuration.ModelProviderMapping mapping)
        {
            // Use the ProviderModelId property (not ProviderModelName)
            return mapping.ProviderModelId;
        }
    }
}
