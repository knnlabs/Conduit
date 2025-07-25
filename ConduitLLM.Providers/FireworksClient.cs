using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.InternalModels;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Client for interacting with Fireworks AI's API.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Fireworks AI provides a fully OpenAI-compatible API with similar endpoints
    /// and request/response formats. This client extends OpenAICompatibleClient to
    /// provide Fireworks-specific configuration and behavior.
    /// </para>
    /// <para>
    /// Fireworks supports multiple model families including Llama, Mixtral, and more.
    /// </para>
    /// </remarks>
    public class FireworksClient : OpenAICompatibleClient
    {
        // Default base URL for Fireworks API
        private const string DefaultFireworksBaseUrl = "https://api.fireworks.ai/inference/v1";

        /// <summary>
        /// Initializes a new instance of the <see cref="FireworksClient"/> class.
        /// </summary>
        /// <param name="credentials">The credentials for accessing the Fireworks API.</param>
        /// <param name="providerModelId">The model identifier to use (e.g., accounts/fireworks/models/llama-v3-8b-instruct).</param>
        /// <param name="logger">The logger to use.</param>
        /// <param name="httpClientFactory">Optional HTTP client factory for advanced usage scenarios.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        public FireworksClient(
            ProviderCredentials credentials,
            string providerModelId,
            ILogger logger,
            IHttpClientFactory? httpClientFactory = null,
            ProviderDefaultModels? defaultModels = null)
            : base(
                credentials,
                providerModelId,
                logger,
                httpClientFactory,
                "Fireworks",
                string.IsNullOrWhiteSpace(credentials.BaseUrl) ? DefaultFireworksBaseUrl : credentials.BaseUrl,
                defaultModels)
        {
        }

        /// <summary>
        /// Configures the HTTP client with Fireworks-specific settings.
        /// </summary>
        /// <param name="client">The HTTP client to configure.</param>
        /// <param name="apiKey">The API key to use for authentication.</param>
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            // Call base implementation to set standard headers
            base.ConfigureHttpClient(client, apiKey);

            // Fireworks uses OpenAI-compatible Authentication with Bearer token
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            // Set any Fireworks-specific headers if needed
            // client.DefaultRequestHeaders.Add("Fireworks-Version", "2023-12-01");
        }

        /// <summary>
        /// Gets a fallback list of models for Fireworks.
        /// </summary>
        /// <returns>A list of common models available on Fireworks.</returns>
        protected override List<ExtendedModelInfo> GetFallbackModels()
        {
            return new List<ExtendedModelInfo>
            {
                ExtendedModelInfo.Create("accounts/fireworks/models/llama-v3-8b-instruct", ProviderName, "accounts/fireworks/models/llama-v3-8b-instruct")
                    .WithName("Llama 3 8B Instruct")
                    .WithCapabilities(new ModelCapabilities
                    {
                        Chat = true,
                        TextGeneration = true,
                        Embeddings = false,
                        ImageGeneration = false,
                        FunctionCalling = true
                    })
                    .WithTokenLimits(new ModelTokenLimits
                    {
                        MaxInputTokens = 16384,
                        MaxOutputTokens = 4096,
                        MaxTotalTokens = 16384
                    }),
                ExtendedModelInfo.Create("accounts/fireworks/models/llama-v3-70b-instruct", ProviderName, "accounts/fireworks/models/llama-v3-70b-instruct")
                    .WithName("Llama 3 70B Instruct")
                    .WithCapabilities(new ModelCapabilities
                    {
                        Chat = true,
                        TextGeneration = true,
                        Embeddings = false,
                        ImageGeneration = false,
                        FunctionCalling = true
                    })
                    .WithTokenLimits(new ModelTokenLimits
                    {
                        MaxInputTokens = 16384,
                        MaxOutputTokens = 4096,
                        MaxTotalTokens = 16384
                    }),
                ExtendedModelInfo.Create("accounts/fireworks/models/mixtral-8x7b-instruct", ProviderName, "accounts/fireworks/models/mixtral-8x7b-instruct")
                    .WithName("Mixtral 8x7B Instruct")
                    .WithCapabilities(new ModelCapabilities
                    {
                        Chat = true,
                        TextGeneration = true,
                        Embeddings = false,
                        ImageGeneration = false,
                        FunctionCalling = true
                    })
                    .WithTokenLimits(new ModelTokenLimits
                    {
                        MaxInputTokens = 32768,
                        MaxOutputTokens = 8192,
                        MaxTotalTokens = 32768
                    }),
                ExtendedModelInfo.Create("accounts/fireworks/models/llama-v2-13b-chat", ProviderName, "accounts/fireworks/models/llama-v2-13b-chat")
                    .WithName("Llama 2 13B Chat")
                    .WithCapabilities(new ModelCapabilities
                    {
                        Chat = true,
                        TextGeneration = true,
                        Embeddings = false,
                        ImageGeneration = false,
                        FunctionCalling = true
                    })
                    .WithTokenLimits(new ModelTokenLimits
                    {
                        MaxInputTokens = 4096,
                        MaxOutputTokens = 4096,
                        MaxTotalTokens = 4096
                    }),
                ExtendedModelInfo.Create("accounts/fireworks/models/firefunction-v1", ProviderName, "accounts/fireworks/models/firefunction-v1")
                    .WithName("FireFunction v1")
                    .WithCapabilities(new ModelCapabilities
                    {
                        Chat = true,
                        TextGeneration = true,
                        Embeddings = false,
                        ImageGeneration = false,
                        FunctionCalling = true
                    })
                    .WithTokenLimits(new ModelTokenLimits
                    {
                        MaxInputTokens = 16384,
                        MaxOutputTokens = 4096,
                        MaxTotalTokens = 16384
                    })
            };
        }

        /// <summary>
        /// Validates credentials for Fireworks.
        /// </summary>
        protected override void ValidateCredentials()
        {
            base.ValidateCredentials();

            // Fireworks requires an API key
            if (string.IsNullOrWhiteSpace(Credentials.ApiKey))
            {
                throw new Core.Exceptions.ConfigurationException($"API key is missing for provider '{ProviderName}'.");
            }
        }

        /// <summary>
        /// Creates embeddings using Fireworks API.
        /// </summary>
        /// <param name="request">The embedding request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An embedding response.</returns>
        /// <remarks>
        /// Note that Fireworks may have a limited set of embedding models available compared to OpenAI.
        /// If embedding request fails, check if the model is supported by Fireworks.
        /// </remarks>
        public override async Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Use the base implementation for the actual API call
            // The model should come from the request or the model mapping system, not be hardcoded
            return await base.CreateEmbeddingAsync(request, apiKey, cancellationToken);
        }

        /// <summary>
        /// Creates images using Fireworks API.
        /// </summary>
        /// <param name="request">The image generation request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An image generation response.</returns>
        /// <exception cref="NotSupportedException">Thrown because Fireworks does not currently support image generation.</exception>
        /// <remarks>
        /// Fireworks does not currently support image generation, so this method throws <see cref="NotSupportedException"/>.
        /// </remarks>
        public override Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            Logger.LogWarning("Image generation is not supported by Fireworks");
            return Task.FromException<ImageGenerationResponse>(
                new NotSupportedException("Image generation is not supported by Fireworks"));
        }
    }
}
