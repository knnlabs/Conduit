using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Routing
{
    /// <summary>
    /// Default implementation of ILLMClientFactory that creates and caches LLM clients
    /// </summary>
    public class DefaultLLMClientFactory : ILLMClientFactory
    {
        private readonly ConduitRegistry _registry;
        private readonly IProviderCredentialService _credentialService;
        private readonly IModelProviderMappingService _mappingService;
        private readonly ILogger<DefaultLLMClientFactory> _logger;
        private readonly ConcurrentDictionary<string, ILLMClient> _clientCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, ILLMClient> _providerClientCache = new(StringComparer.OrdinalIgnoreCase);

        // Provider factory mapping for supported providers
        private readonly Dictionary<string, Func<string, ILLMClient>> _providerFactories = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates a new instance of DefaultLLMClientFactory
        /// </summary>
        /// <param name="registry">Conduit registry for model configuration</param>
        /// <param name="credentialService">Provider credential service</param>
        /// <param name="mappingService">Model mapping service</param>
        /// <param name="logger">Logger instance</param>
        public DefaultLLMClientFactory(
            ConduitRegistry registry,
            IProviderCredentialService credentialService,
            IModelProviderMappingService mappingService,
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
        public ILLMClient GetClientByProvider(string providerName)
        {
            if (string.IsNullOrEmpty(providerName))
            {
                throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));
            }

            // Try to get from the provider cache first
            return _providerClientCache.GetOrAdd(providerName, CreateClientByProvider);
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
        /// Creates a new LLM client for the specified model alias
        /// </summary>
        private ILLMClient CreateClient(string modelAlias)
        {
            try
            {
                // Get the mapping for this model alias
                var mapping = _mappingService.GetMappingByModelAliasAsync(modelAlias).GetAwaiter().GetResult();
                
                if (mapping == null)
                {
                    _logger.LogWarning("No mapping found for model alias {ModelAlias}", modelAlias);
                    throw new ConfigurationException($"No mapping found for model alias '{modelAlias}'");
                }
                
                // Get the provider name - we use a default provider name for development since the actual
                // property path might be different in the ModelProviderMapping implementation
                string providerName = ModelProviderMappingAdapter.GetProviderName(mapping);
                
                _logger.LogInformation("Using provider {Provider} for model {Model}", providerName, modelAlias);
                
                // Check if we have a factory for this provider
                if (!_providerFactories.TryGetValue(providerName, out var factory))
                {
                    _logger.LogWarning("No factory found for provider {ProviderName}", providerName);
                    
                    // Return a placeholder client for demonstration purposes
                    return new PlaceholderLLMClient(ModelProviderMappingAdapter.GetProviderModelName(mapping), providerName, _logger);
                }
                
                // Create the client using the factory function
                return factory(ModelProviderMappingAdapter.GetProviderModelName(mapping));
            }
            catch (Exception ex) when (ex is not ConfigurationException)
            {
                _logger.LogError(ex, "Error creating client for model {ModelAlias}", modelAlias);
                throw new ConfigurationException($"Error creating client for model '{modelAlias}'", ex);
            }
        }

        /// <summary>
        /// Creates a new LLM client for the specified provider
        /// </summary>
        private ILLMClient CreateClientByProvider(string providerName)
        {
            try
            {
                // Get provider credentials
                var credentials = _credentialService.GetCredentialByProviderNameAsync(providerName).GetAwaiter().GetResult()
                    ?? throw new ConfigurationException($"No credentials found for provider '{providerName}'");
                
                // Check if we have a factory for this provider
                if (!_providerFactories.TryGetValue(providerName, out var factory))
                {
                    _logger.LogWarning("No factory found for provider {ProviderName}", providerName);
                    
                    // Return a placeholder client for demonstration purposes
                    return new PlaceholderLLMClient(null, providerName, _logger);
                }
                
                // Create the client using the factory function
                return factory(providerName);
            }
            catch (Exception ex) when (ex is not ConfigurationException)
            {
                _logger.LogError(ex, "Error creating client for provider {ProviderName}", providerName);
                throw new ConfigurationException($"Error creating client for provider '{providerName}'", ex);
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
        }
    }

    /// <summary>
    /// A placeholder LLM client implementation for testing purposes
    /// </summary>
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
    }

    /// <summary>
    /// Adapter class to handle different ModelProviderMapping implementations
    /// </summary>
    internal static class ModelProviderMappingAdapter
    {
        /// <summary>
        /// Get the provider name from a mapping
        /// </summary>
        public static string GetProviderName(ConduitLLM.Configuration.ModelProviderMapping mapping)
        {
            // Get the provider name from the mapping
            return mapping.ProviderName;
        }

        /// <summary>
        /// Get the provider model name from a mapping
        /// </summary>
        public static string GetProviderModelName(ConduitLLM.Configuration.ModelProviderMapping mapping)
        {
            // Use the ProviderModelId property (not ProviderModelName)
            return mapping.ProviderModelId;
        }
    }
}
