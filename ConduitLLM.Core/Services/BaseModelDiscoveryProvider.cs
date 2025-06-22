using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Base class for model discovery providers with common functionality.
    /// </summary>
    public abstract class BaseModelDiscoveryProvider : IModelDiscoveryProvider
    {
        protected readonly HttpClient HttpClient;
        protected readonly ILogger Logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseModelDiscoveryProvider"/> class.
        /// </summary>
        /// <param name="httpClient">HTTP client for making API requests.</param>
        /// <param name="logger">Logger for diagnostic information.</param>
        protected BaseModelDiscoveryProvider(HttpClient httpClient, ILogger logger)
        {
            HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public abstract string ProviderName { get; }

        /// <inheritdoc />
        public abstract bool SupportsDiscovery { get; }

        /// <inheritdoc />
        public abstract Task<List<ModelMetadata>> DiscoverModelsAsync(CancellationToken cancellationToken = default);

        /// <inheritdoc />
        public virtual async Task<ModelMetadata?> GetModelMetadataAsync(string modelId, CancellationToken cancellationToken = default)
        {
            try
            {
                var allModels = await DiscoverModelsAsync(cancellationToken);
                return allModels.Find(m => string.Equals(m.ModelId, modelId, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting metadata for model {ModelId} from provider {Provider}", modelId, ProviderName);
                return null;
            }
        }

        /// <inheritdoc />
        public virtual async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Simple availability check - try to discover models
                var models = await DiscoverModelsAsync(cancellationToken);
                return models.Count > 0;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Provider {Provider} discovery API is not available", ProviderName);
                return false;
            }
        }

        /// <summary>
        /// Creates a fallback ModelMetadata when discovery fails.
        /// Uses the existing hardcoded patterns as backup.
        /// </summary>
        /// <param name="modelId">The model ID to create fallback metadata for.</param>
        /// <param name="reason">Reason why fallback was needed.</param>
        /// <returns>Fallback model metadata.</returns>
        protected ModelMetadata CreateFallbackMetadata(string modelId, string reason)
        {
            Logger.LogWarning("Creating fallback metadata for {ModelId} from {Provider}: {Reason}", 
                modelId, ProviderName, reason);

            return new ModelMetadata
            {
                ModelId = modelId,
                DisplayName = modelId,
                Provider = ProviderName,
                Capabilities = GetFallbackCapabilities(modelId),
                Source = ModelDiscoverySource.HardcodedPattern,
                LastUpdated = DateTime.UtcNow,
                Warnings = new List<string> { $"Using fallback metadata: {reason}" }
            };
        }

        /// <summary>
        /// Gets fallback capabilities based on model name patterns.
        /// This uses the same logic as the existing ProviderDiscoveryService.
        /// </summary>
        /// <param name="modelId">The model ID to infer capabilities for.</param>
        /// <returns>Inferred capabilities.</returns>
        protected virtual ModelCapabilities GetFallbackCapabilities(string modelId)
        {
            var lowerModelId = modelId.ToLowerInvariant();

            // Use similar patterns to existing ProviderDiscoveryService
            return ProviderName.ToLowerInvariant() switch
            {
                "openai" => GetOpenAIFallbackCapabilities(lowerModelId),
                "anthropic" => GetAnthropicFallbackCapabilities(lowerModelId),
                "google" => GetGoogleFallbackCapabilities(lowerModelId),
                "openrouter" => GetOpenRouterFallbackCapabilities(lowerModelId),
                _ => GetGenericFallbackCapabilities()
            };
        }

        /// <summary>
        /// Gets fallback capabilities for OpenAI models.
        /// </summary>
        protected virtual ModelCapabilities GetOpenAIFallbackCapabilities(string modelId)
        {
            if (modelId.Contains("gpt-4"))
            {
                return new ModelCapabilities
                {
                    Chat = true,
                    ChatStream = true,
                    Vision = modelId.Contains("vision") || modelId.Contains("gpt-4-turbo") || modelId.Contains("gpt-4o"),
                    FunctionCalling = true,
                    ToolUse = true,
                    JsonMode = true,
                    MaxTokens = modelId.Contains("32k") ? 32768 : (modelId.Contains("turbo") ? 128000 : 8192),
                    MaxOutputTokens = 4096
                };
            }

            if (modelId.Contains("gpt-3.5"))
            {
                return new ModelCapabilities
                {
                    Chat = true,
                    ChatStream = true,
                    FunctionCalling = true,
                    ToolUse = true,
                    JsonMode = true,
                    MaxTokens = 16385,
                    MaxOutputTokens = 4096
                };
            }

            if (modelId.Contains("dall-e"))
            {
                return new ModelCapabilities
                {
                    ImageGeneration = true,
                    SupportedImageSizes = modelId.Contains("3") 
                        ? new List<string> { "1024x1024", "1792x1024", "1024x1792" }
                        : new List<string> { "256x256", "512x512", "1024x1024" }
                };
            }

            if (modelId.Contains("text-embedding"))
            {
                return new ModelCapabilities { Embeddings = true };
            }

            // Default OpenAI capabilities
            return new ModelCapabilities { Chat = true, ChatStream = true };
        }

        /// <summary>
        /// Gets fallback capabilities for Anthropic models.
        /// </summary>
        protected virtual ModelCapabilities GetAnthropicFallbackCapabilities(string modelId)
        {
            return new ModelCapabilities
            {
                Chat = true,
                ChatStream = true,
                Vision = modelId.Contains("vision") || modelId.Contains("claude-3"),
                ToolUse = true,
                JsonMode = false,
                MaxTokens = modelId.Contains("200k") ? 200000 : 100000,
                MaxOutputTokens = 4096
            };
        }

        /// <summary>
        /// Gets fallback capabilities for Google models.
        /// </summary>
        protected virtual ModelCapabilities GetGoogleFallbackCapabilities(string modelId)
        {
            return new ModelCapabilities
            {
                Chat = true,
                ChatStream = true,
                Vision = true,
                VideoUnderstanding = modelId.Contains("pro"),
                FunctionCalling = false,
                ToolUse = false,
                MaxTokens = modelId.Contains("1.5") ? 1048576 : 32768,
                MaxOutputTokens = 8192
            };
        }

        /// <summary>
        /// Gets fallback capabilities for OpenRouter models.
        /// </summary>
        protected virtual ModelCapabilities GetOpenRouterFallbackCapabilities(string modelId)
        {
            // OpenRouter models are typically chat models
            return new ModelCapabilities
            {
                Chat = true,
                ChatStream = true,
                FunctionCalling = true,
                ToolUse = true
            };
        }

        /// <summary>
        /// Gets generic fallback capabilities for unknown providers.
        /// </summary>
        protected virtual ModelCapabilities GetGenericFallbackCapabilities()
        {
            return new ModelCapabilities
            {
                Chat = true,
                ChatStream = true
            };
        }

        /// <summary>
        /// Handles common HTTP request errors and provides helpful error messages.
        /// </summary>
        /// <param name="ex">The exception that occurred.</param>
        /// <param name="operation">The operation that was being performed.</param>
        protected void LogHttpError(Exception ex, string operation)
        {
            switch (ex)
            {
                case HttpRequestException httpEx:
                    Logger.LogError(httpEx, "HTTP error during {Operation} for {Provider}: {Message}", 
                        operation, ProviderName, httpEx.Message);
                    break;
                case TaskCanceledException timeoutEx:
                    Logger.LogError(timeoutEx, "Timeout during {Operation} for {Provider}", 
                        operation, ProviderName);
                    break;
                default:
                    Logger.LogError(ex, "Unexpected error during {Operation} for {Provider}: {Message}", 
                        operation, ProviderName, ex.Message);
                    break;
            }
        }
    }
}