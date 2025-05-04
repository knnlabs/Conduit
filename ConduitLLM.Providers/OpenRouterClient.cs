using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Utilities;
using ConduitLLM.Providers.InternalModels;
using ConduitLLM.Providers.InternalModels.OpenAIModels;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Client for interacting with the OpenRouter API, which provides a unified interface to multiple LLM providers.
    /// </summary>
    /// <remarks>
    /// OpenRouter provides access to models from various providers through a unified API that's compatible with OpenAI's.
    /// </remarks>
    public class OpenRouterClient : OpenAICompatibleClient
    {
        private const string DefaultOpenRouterApiBase = "https://openrouter.ai/api/v1";
        private readonly string _appName;
        private readonly string _appUrl;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenRouterClient"/> class.
        /// </summary>
        /// <param name="credentials">The provider credentials.</param>
        /// <param name="providerModelId">The provider's model identifier.</param>
        /// <param name="logger">The logger to use.</param>
        /// <param name="httpClientFactory">Optional HTTP client factory.</param>
        /// <param name="appName">The name of the application making the request. Used for OpenRouter analytics.</param>
        /// <param name="appUrl">The URL of the application making the request. Used for OpenRouter analytics.</param>
        public OpenRouterClient(
            ProviderCredentials credentials,
            string providerModelId,
            ILogger<OpenRouterClient> logger,
            IHttpClientFactory? httpClientFactory = null,
            string appName = "ConduitLLM",
            string appUrl = "https://conduit-llm.com")
            : base(
                  EnsureOpenRouterCredentials(credentials),
                  providerModelId,
                  logger,
                  httpClientFactory,
                  "openrouter",
                  DetermineBaseUrl(credentials))
        {
            _appName = appName;
            _appUrl = appUrl;
        }

        private static ProviderCredentials EnsureOpenRouterCredentials(ProviderCredentials credentials)
        {
            if (credentials == null)
            {
                throw new ArgumentNullException(nameof(credentials));
            }

            if (string.IsNullOrWhiteSpace(credentials.ApiKey))
            {
                throw new ConfigurationException("API key is missing for OpenRouter provider.");
            }

            return credentials;
        }

        private static string DetermineBaseUrl(ProviderCredentials credentials)
        {
            return string.IsNullOrWhiteSpace(credentials.ApiBase)
                ? DefaultOpenRouterApiBase
                : credentials.ApiBase.TrimEnd('/');
        }

        /// <inheritdoc />
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            base.ConfigureHttpClient(client, apiKey);
            
            // Add OpenRouter-specific headers
            client.DefaultRequestHeaders.Add("HTTP-Referer", _appUrl);
            client.DefaultRequestHeaders.Add("X-Title", _appName);
        }

        /// <inheritdoc />
        public override async Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteApiRequestAsync(async () =>
                {
                    using var client = CreateHttpClient(apiKey);
                    
                    var endpoint = GetModelsEndpoint();
                    
                    Logger.LogDebug("Getting available models from OpenRouter at {Endpoint}", endpoint);
                    
                    var response = await HttpClientHelper.SendJsonRequestAsync<object, OpenRouterModelsResponse>(
                        client,
                        HttpMethod.Get,
                        endpoint,
                        null,
                        CreateStandardHeaders(apiKey),
                        DefaultJsonOptions,
                        Logger,
                        cancellationToken);
                    
                    if (response.Data == null || !response.Data.Any())
                    {
                        Logger.LogWarning("OpenRouter API returned null/empty data for models.");
                        return new List<ExtendedModelInfo>();
                    }
                    
                    return response.Data
                        .Where(m => !string.IsNullOrEmpty(m.Id))
                        .Select(m => ExtendedModelInfo.Create(m.Id, ProviderName, m.Id))
                        .ToList();
                }, "GetModels", cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to retrieve models from OpenRouter API. Returning known models.");
                return GetFallbackModels();
            }
        }

        /// <summary>
        /// Gets a fallback list of models for OpenRouter.
        /// </summary>
        /// <returns>A list of known models for OpenRouter.</returns>
        protected override List<ExtendedModelInfo> GetFallbackModels()
        {
            return new List<ExtendedModelInfo>
            {
                ExtendedModelInfo.Create("anthropic/claude-3-opus-20240229", ProviderName, "anthropic/claude-3-opus-20240229"),
                ExtendedModelInfo.Create("anthropic/claude-3-sonnet-20240229", ProviderName, "anthropic/claude-3-sonnet-20240229"),
                ExtendedModelInfo.Create("anthropic/claude-3-haiku-20240307", ProviderName, "anthropic/claude-3-haiku-20240307"),
                ExtendedModelInfo.Create("openai/gpt-4o", ProviderName, "openai/gpt-4o"),
                ExtendedModelInfo.Create("openai/gpt-4-turbo", ProviderName, "openai/gpt-4-turbo"),
                ExtendedModelInfo.Create("openai/gpt-3.5-turbo", ProviderName, "openai/gpt-3.5-turbo"),
                ExtendedModelInfo.Create("meta-llama/llama-3-70b-instruct", ProviderName, "meta-llama/llama-3-70b-instruct"),
                ExtendedModelInfo.Create("meta-llama/llama-3-8b-instruct", ProviderName, "meta-llama/llama-3-8b-instruct"),
                ExtendedModelInfo.Create("google/gemini-pro", ProviderName, "google/gemini-pro"),
                ExtendedModelInfo.Create("mistral/mistral-large", ProviderName, "mistral/mistral-large"),
                ExtendedModelInfo.Create("mistral/mistral-medium", ProviderName, "mistral/mistral-medium"),
                ExtendedModelInfo.Create("mistral/mistral-small", ProviderName, "mistral/mistral-small")
            };
        }
    }

    /// <summary>
    /// Response DTO for OpenRouter models endpoint.
    /// </summary>
    internal class OpenRouterModelsResponse
    {
        /// <summary>
        /// List of available models.
        /// </summary>
        public List<OpenRouterModel> Data { get; set; } = new List<OpenRouterModel>();
    }

    /// <summary>
    /// DTO for OpenRouter model information.
    /// </summary>
    internal class OpenRouterModel
    {
        /// <summary>
        /// The model ID.
        /// </summary>
        public string Id { get; set; } = string.Empty;
    }
}