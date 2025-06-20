using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core
{
    /// <summary>
    /// Registry for managing LLM provider factories and their capabilities.
    /// Enables dynamic provider registration and client creation.
    /// </summary>
    public class ConduitRegistry : IProviderRegistry
    {
        private readonly ConcurrentDictionary<string, ProviderRegistration> _providers = new(StringComparer.OrdinalIgnoreCase);
        private readonly ILogger<ConduitRegistry> _logger;

        /// <summary>
        /// Initializes a new instance of the ConduitRegistry.
        /// </summary>
        /// <param name="logger">Logger instance for the registry.</param>
        public ConduitRegistry(ILogger<ConduitRegistry> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.LogInformation("ConduitRegistry initialized");
            
            // Initialize with built-in providers
            RegisterBuiltInProviders();
        }

        /// <summary>
        /// Registers a provider factory function.
        /// </summary>
        /// <typeparam name="TClient">The type of LLM client this factory creates.</typeparam>
        /// <param name="providerName">The name of the provider (e.g., "openai", "anthropic").</param>
        /// <param name="factory">Factory function that creates the client instance.</param>
        /// <param name="capabilities">Optional capabilities of this provider.</param>
        public void RegisterProvider<TClient>(
            string providerName, 
            Func<ProviderCredentials, string, IServiceProvider, TClient> factory,
            ProviderCapabilities? capabilities = null) 
            where TClient : ILLMClient
        {
            if (string.IsNullOrWhiteSpace(providerName))
                throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));
            
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            var registration = new ProviderRegistration
            {
                ProviderName = providerName,
                Factory = (credentials, modelId, serviceProvider) => factory(credentials, modelId, serviceProvider),
                Capabilities = capabilities ?? new ProviderCapabilities(),
                ClientType = typeof(TClient)
            };

            _providers.AddOrUpdate(providerName, registration, (key, existing) => registration);
            
            _logger.LogInformation("Registered provider: {ProviderName} with client type: {ClientType}", 
                providerName, typeof(TClient).Name);
        }

        /// <summary>
        /// Creates an LLM client for the specified provider.
        /// </summary>
        /// <param name="providerName">The name of the provider.</param>
        /// <param name="credentials">Provider credentials.</param>
        /// <param name="modelId">The model identifier.</param>
        /// <param name="serviceProvider">Service provider for dependency injection.</param>
        /// <returns>An instance of the LLM client.</returns>
        public ILLMClient CreateClient(string providerName, ProviderCredentials credentials, string modelId, IServiceProvider serviceProvider)
        {
            if (string.IsNullOrWhiteSpace(providerName))
                throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));

            if (credentials == null)
                throw new ArgumentNullException(nameof(credentials));

            if (!_providers.TryGetValue(providerName, out var registration))
            {
                throw new UnsupportedProviderException($"Provider '{providerName}' is not registered. Available providers: {string.Join(", ", GetRegisteredProviders())}");
            }

            try
            {
                var client = registration.Factory(credentials, modelId ?? "default", serviceProvider);
                
                _logger.LogDebug("Created client for provider: {ProviderName}, model: {ModelId}", providerName, modelId);
                
                return client;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create client for provider: {ProviderName}", providerName);
                throw new ConduitException($"Failed to create client for provider '{providerName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Checks if a provider is registered.
        /// </summary>
        /// <param name="providerName">The name of the provider.</param>
        /// <returns>True if the provider is registered, false otherwise.</returns>
        public bool IsProviderRegistered(string providerName)
        {
            return !string.IsNullOrWhiteSpace(providerName) && _providers.ContainsKey(providerName);
        }

        /// <summary>
        /// Gets all registered provider names.
        /// </summary>
        /// <returns>An enumerable of registered provider names.</returns>
        public IEnumerable<string> GetRegisteredProviders()
        {
            return _providers.Keys.ToList();
        }

        /// <summary>
        /// Gets the capabilities of a registered provider.
        /// </summary>
        /// <param name="providerName">The name of the provider.</param>
        /// <returns>The provider capabilities.</returns>
        public ProviderCapabilities GetProviderCapabilities(string providerName)
        {
            if (!_providers.TryGetValue(providerName, out var registration))
            {
                throw new ArgumentException($"Provider '{providerName}' is not registered", nameof(providerName));
            }

            return registration.Capabilities;
        }

        /// <summary>
        /// Gets provider registration information.
        /// </summary>
        /// <param name="providerName">The name of the provider.</param>
        /// <returns>The provider registration information.</returns>
        public ProviderRegistration? GetProviderRegistration(string providerName)
        {
            _providers.TryGetValue(providerName, out var registration);
            return registration;
        }

        /// <summary>
        /// Registers built-in providers that are always available.
        /// </summary>
        private void RegisterBuiltInProviders()
        {
            // Note: In a real implementation, these would use actual client factories
            // For now, we register placeholder factories since the real factories
            // are in the ConduitLLM.Providers assembly and would create circular dependencies
            
            var defaultCapabilities = new ProviderCapabilities
            {
                SupportsChatCompletion = true,
                SupportsStreaming = true,
                SupportsEmbeddings = false,
                SupportsImageGeneration = false,
                SupportsAudio = false,
                MaxContextLength = 4096
            };

            // Register common providers with their typical capabilities
            RegisterProviderPlaceholder("openai", new ProviderCapabilities
            {
                SupportsChatCompletion = true,
                SupportsStreaming = true,
                SupportsEmbeddings = true, 
                SupportsImageGeneration = true,
                SupportsAudio = false,
                MaxContextLength = 128000
            });
            
            RegisterProviderPlaceholder("anthropic", new ProviderCapabilities
            {
                SupportsChatCompletion = true,
                SupportsStreaming = true,
                SupportsEmbeddings = false,
                SupportsImageGeneration = false,
                SupportsAudio = false,
                MaxContextLength = 200000
            });
            
            RegisterProviderPlaceholder("cohere", new ProviderCapabilities
            {
                SupportsChatCompletion = true,
                SupportsStreaming = true,
                SupportsEmbeddings = true,
                SupportsImageGeneration = false,
                SupportsAudio = false,
                MaxContextLength = 8192
            });
            
            RegisterProviderPlaceholder("google", new ProviderCapabilities
            {
                SupportsChatCompletion = true,
                SupportsStreaming = true,
                SupportsEmbeddings = false,
                SupportsImageGeneration = false,
                SupportsAudio = false,
                MaxContextLength = 32000
            });
            
            RegisterProviderPlaceholder("mistral", defaultCapabilities);
            RegisterProviderPlaceholder("groq", defaultCapabilities);
            RegisterProviderPlaceholder("openrouter", defaultCapabilities);
            RegisterProviderPlaceholder("huggingface", new ProviderCapabilities
            {
                SupportsChatCompletion = true,
                SupportsStreaming = true,
                SupportsEmbeddings = false,
                SupportsImageGeneration = true,
                SupportsAudio = false,
                MaxContextLength = 4096
            });
            
            RegisterProviderPlaceholder("bedrock", new ProviderCapabilities
            {
                SupportsChatCompletion = true,
                SupportsStreaming = true,
                SupportsEmbeddings = true, 
                SupportsImageGeneration = true,
                SupportsAudio = false,
                MaxContextLength = 4096
            });

            _logger.LogInformation("Registered {Count} built-in providers", _providers.Count);
        }

        /// <summary>
        /// Helper method to register placeholder providers.
        /// </summary>
        private void RegisterProviderPlaceholder(string providerName, ProviderCapabilities capabilities)
        {
            var registration = new ProviderRegistration
            {
                ProviderName = providerName,
                Factory = (credentials, modelId, serviceProvider) => 
                    throw new NotImplementedException($"Placeholder factory for {providerName}. Use the real LLMClientFactory for production."),
                Capabilities = capabilities,
                ClientType = typeof(ILLMClient)
            };

            _providers.TryAdd(providerName, registration);
        }
    }

    /// <summary>
    /// Interface for provider registry operations.
    /// </summary>
    public interface IProviderRegistry
    {
        /// <summary>
        /// Registers a provider factory function.
        /// </summary>
        void RegisterProvider<TClient>(string providerName, Func<ProviderCredentials, string, IServiceProvider, TClient> factory, ProviderCapabilities? capabilities = null) where TClient : ILLMClient;
        
        /// <summary>
        /// Creates an LLM client for the specified provider.
        /// </summary>
        ILLMClient CreateClient(string providerName, ProviderCredentials credentials, string modelId, IServiceProvider serviceProvider);
        
        /// <summary>
        /// Checks if a provider is registered.
        /// </summary>
        bool IsProviderRegistered(string providerName);
        
        /// <summary>
        /// Gets all registered provider names.
        /// </summary>
        IEnumerable<string> GetRegisteredProviders();
        
        /// <summary>
        /// Gets the capabilities of a registered provider.
        /// </summary>
        ProviderCapabilities GetProviderCapabilities(string providerName);
    }

    /// <summary>
    /// Information about a registered provider.
    /// </summary>
    public class ProviderRegistration
    {
        /// <summary>
        /// The name of the provider.
        /// </summary>
        public string ProviderName { get; set; } = null!;

        /// <summary>
        /// Factory function for creating client instances.
        /// </summary>
        public Func<ProviderCredentials, string, IServiceProvider, ILLMClient> Factory { get; set; } = null!;

        /// <summary>
        /// Capabilities of this provider.
        /// </summary>
        public ProviderCapabilities Capabilities { get; set; } = new();

        /// <summary>
        /// The type of client this factory creates.
        /// </summary>
        public Type ClientType { get; set; } = typeof(ILLMClient);
    }

    /// <summary>
    /// Capabilities of an LLM provider.
    /// </summary>
    public class ProviderCapabilities
    {
        /// <summary>
        /// Whether the provider supports chat completions.
        /// </summary>
        public bool SupportsChatCompletion { get; set; } = true;

        /// <summary>
        /// Whether the provider supports streaming responses.
        /// </summary>
        public bool SupportsStreaming { get; set; } = false;

        /// <summary>
        /// Whether the provider supports embeddings.
        /// </summary>
        public bool SupportsEmbeddings { get; set; } = false;

        /// <summary>
        /// Whether the provider supports image generation.
        /// </summary>
        public bool SupportsImageGeneration { get; set; } = false;

        /// <summary>
        /// Whether the provider supports audio processing.
        /// </summary>
        public bool SupportsAudio { get; set; } = false;

        /// <summary>
        /// Maximum context length in tokens.
        /// </summary>
        public int MaxContextLength { get; set; } = 4096;

        /// <summary>
        /// Supported model names/patterns.
        /// </summary>
        public List<string> SupportedModels { get; set; } = new();
    }
}
