using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Providers.OpenRouter
{
    /// <summary>
    /// Client for interacting with OpenRouter API, providing access to multiple AI models through a unified interface.
    /// OpenRouter is a meta-provider that routes requests to various underlying providers.
    /// </summary>
    /// <remarks>
    /// OpenRouter features:
    /// <list type="bullet">
    /// <item>Access to models from multiple providers through a single API</item>
    /// <item>Automatic failover and load balancing</item>
    /// <item>Unified billing across providers</item>
    /// <item>Standard OpenAI-compatible API format</item>
    /// </list>
    /// 
    /// Configuration example:
    /// <code>
    /// {
    ///   "ProviderName": "openrouter",
    ///   "ApiKey": "sk-or-...",
    ///   "BaseUrl": "https://openrouter.ai/api/v1"
    /// }
    /// </code>
    /// </remarks>
    public class OpenRouterClient : ConduitLLM.Providers.Providers.OpenAICompatible.OpenAICompatibleClient
    {
        /// <summary>
        /// OpenRouter-specific constants for API configuration and endpoints.
        /// </summary>
        private static class Constants
        {
            /// <summary>
            /// OpenRouter-specific URLs and API configuration.
            /// </summary>
            public static class Urls
            {
                /// <summary>
                /// The base URL for OpenRouter API requests.
                /// </summary>
                public const string BaseUrl = "https://openrouter.ai/api/v1/";

                /// <summary>
                /// The full API endpoint for OpenRouter requests.
                /// </summary>
                public const string ApiEndpoint = "https://openrouter.ai/api/v1";
            }

            /// <summary>
            /// API endpoints relative to the base URL.
            /// </summary>
            public static class Endpoints
            {
                /// <summary>
                /// Endpoint for chat completions.
                /// </summary>
                public const string ChatCompletions = "/chat/completions";

                /// <summary>
                /// Endpoint for listing available models.
                /// </summary>
                public const string Models = "/models";
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenRouterClient"/> class.
        /// </summary>
        /// <param name="credentials">OpenRouter API credentials including API key and optional base URL.</param>
        /// <param name="modelId">The ID of the model to use for requests (e.g., "openai/gpt-4", "anthropic/claude-2").</param>
        /// <param name="logger">Logger instance for diagnostic output.</param>
        /// <param name="httpClientFactory">Optional HTTP client factory for creating HttpClient instances.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        public OpenRouterClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string modelId,
            ILogger<OpenRouterClient> logger,
            IHttpClientFactory? httpClientFactory = null,
            ProviderDefaultModels? defaultModels = null)
            : base(
                provider,
                keyCredential,
                modelId,
                logger,
                httpClientFactory,
                "OpenRouter",
                baseUrl: null,
                defaultModels: defaultModels)
        {
        }

        /// <summary>
        /// Override to provide the fixed models endpoint URL without double slashes.
        /// </summary>
        /// <returns>The full URL for the models endpoint.</returns>
        protected override string GetModelsEndpoint()
        {
            // Fix the double slash by using a full URL without relying on BaseUrl with trailing slash
            return Constants.Urls.ApiEndpoint + Constants.Endpoints.Models;
        }

        /// <summary>
        /// Override GetModelsAsync to handle OpenRouter's simplified response format.
        /// </summary>
        public override async Task<List<ConduitLLM.Providers.Common.Models.ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteApiRequestAsync(async () =>
                {
                    using var client = CreateHttpClient(apiKey);

                    var endpoint = GetModelsEndpoint();

                    Logger.LogDebug("Getting available models from {Provider} at {Endpoint}", ProviderName, endpoint);

                    var response = await client.GetAsync(endpoint, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync(cancellationToken);

                    // OpenRouter returns a simplified format with just model IDs
                    var jsonDocument = JsonDocument.Parse(content);
                    var models = new List<ConduitLLM.Providers.Common.Models.ExtendedModelInfo>();

                    if (jsonDocument.RootElement.TryGetProperty("data", out var dataArray))
                    {
                        foreach (var item in dataArray.EnumerateArray())
                        {
                            if (item.TryGetProperty("id", out var idProperty))
                            {
                                var modelId = idProperty.GetString();
                                if (!string.IsNullOrEmpty(modelId))
                                {
                                    models.Add(ConduitLLM.Providers.Common.Models.ExtendedModelInfo.Create(modelId, ProviderName, modelId));
                                }
                            }
                        }
                    }

                    Logger.LogInformation($"OpenRouter returned {models.Count} models");
                    return models;
                }, "GetModels", cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to retrieve models from {Provider} API.", ProviderName);
                throw;
            }
        }

        /// <summary>
        /// Override to implement embedding for OpenRouter API.
        /// </summary>
        /// <param name="request">The embedding request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An embedding response.</returns>
        public override Task<ConduitLLM.Core.Models.EmbeddingResponse> CreateEmbeddingAsync(
            ConduitLLM.Core.Models.EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // This is a minimal implementation - override with proper support if needed
            throw new NotSupportedException("Embeddings are not supported in the OpenRouter client");
        }

        /// <summary>
        /// Override to implement image generation for OpenRouter API.
        /// </summary>
        /// <param name="request">The image generation request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An image generation response.</returns>
        public override Task<ConduitLLM.Core.Models.ImageGenerationResponse> CreateImageAsync(
            ConduitLLM.Core.Models.ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // This is a minimal implementation - override with proper support if needed
            throw new NotSupportedException("Image generation is not supported in the OpenRouter client");
        }



        /// <summary>
        /// Configures the HTTP client with OpenRouter-specific settings and headers.
        /// </summary>
        /// <param name="client">The HTTP client to configure.</param>
        /// <param name="apiKey">The API key for authentication.</param>
        /// <remarks>
        /// OpenRouter requires specific headers:
        /// <list type="bullet">
        /// <item>HTTP-Referer: Identifies the source application</item>
        /// <item>X-Title: Optional title for request tracking</item>
        /// </list>
        /// </remarks>
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            base.ConfigureHttpClient(client, apiKey);

            // Add OpenRouter-specific headers
            client.DefaultRequestHeaders.Add("HTTP-Referer", "https://conduit-llm.com");
            client.DefaultRequestHeaders.Add("X-Title", "ConduitLLM");
        }

        /// <summary>
        /// Gets the chat completion endpoint URL.
        /// </summary>
        /// <returns>The full URL for the chat completions endpoint.</returns>
        /// <remarks>
        /// OpenRouter uses a non-standard path structure compared to vanilla OpenAI API.
        /// </remarks>
        protected override string GetChatCompletionEndpoint()
        {
            // Fix: Use the correct path structure for OpenRouter
            return Constants.Urls.ApiEndpoint + Constants.Endpoints.ChatCompletions;
        }
    }
}
