using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.InternalModels;
using ConduitLLM.Providers.InternalModels.OpenAIModels;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Client for interacting with the OpenRouter API.
    /// </summary>
    public class OpenRouterClient : OpenAICompatibleClient
    {
        private const string DefaultApiBase = "https://openrouter.ai/api/v1/";

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenRouterClientRevised"/> class.
        /// </summary>
        /// <param name="credentials">The provider credentials.</param>
        /// <param name="providerModelId">The provider's model identifier.</param>
        /// <param name="logger">The logger to use.</param>
        /// <param name="httpClientFactory">Optional HTTP client factory.</param>
        public OpenRouterClient(
            ProviderCredentials credentials,
            string providerModelId,
            ILogger<OpenRouterClient> logger,
            IHttpClientFactory? httpClientFactory = null)
            : base(
                EnsureOpenRouterCredentials(credentials),
                providerModelId,
                logger,
                httpClientFactory,
                "openrouter",
                DetermineBaseUrl(credentials))
        {
        }

        private static ProviderCredentials EnsureOpenRouterCredentials(ProviderCredentials credentials)
        {
            if (credentials == null)
            {
                throw new ArgumentNullException(nameof(credentials));
            }

            if (string.IsNullOrWhiteSpace(credentials.ApiKey))
            {
                throw new ConfigurationException("API key is required for OpenRouter API");
            }

            return credentials;
        }

        private static string DetermineBaseUrl(ProviderCredentials credentials)
        {
            return string.IsNullOrWhiteSpace(credentials.ApiBase)
                ? DefaultApiBase
                : credentials.ApiBase.TrimEnd('/') + "/";
        }

        /// <inheritdoc />
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            base.ConfigureHttpClient(client, apiKey);
            
            // Add OpenRouter-specific headers
            client.DefaultRequestHeaders.Add("HTTP-Referer", "https://conduit-llm.com");
            client.DefaultRequestHeaders.Add("X-Title", "ConduitLLM");
        }

        /// <inheritdoc />
        public override async Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                Logger.LogInformation("Listing OpenRouter models");
                
                using var client = CreateHttpClient(apiKey);
                string endpoint = "models";
                
                using var requestMessage = new HttpRequestMessage(HttpMethod.Get, endpoint);
                using var response = await client.SendAsync(requestMessage, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await ReadErrorContentAsync(response, cancellationToken);
                    Logger.LogError("OpenRouter API list models failed: {StatusCode}. Response: {Response}", 
                        response.StatusCode, errorContent);
                    throw new LLMCommunicationException($"OpenRouter list models failed: {response.ReasonPhrase} ({response.StatusCode})");
                }
                
                // Custom DTO for OpenRouter /models response
                var openRouterModelsResponse = await response.Content.ReadFromJsonAsync<OpenRouterModelsResponse>(
                    options: DefaultJsonOptions,
                    cancellationToken: cancellationToken);
                
                if (openRouterModelsResponse?.Data == null)
                {
                    Logger.LogWarning("OpenRouter API returned null/empty data for models.");
                    return new List<ExtendedModelInfo>();
                }
                
                return openRouterModelsResponse.Data
                    .Where(m => !string.IsNullOrEmpty(m.Id))
                    .Select(m => ExtendedModelInfo.Create(m.Id, ProviderName, m.Id))
                    .ToList();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error listing OpenRouter models");
                return new List<ExtendedModelInfo>(); // Return empty list on error
            }
        }
        
        // DTOs for OpenRouter /models response - nested private classes
        private class OpenRouterModel
        {
            [System.Text.Json.Serialization.JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;
        }

        private class OpenRouterModelsResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("data")]
            public List<OpenRouterModel> Data { get; set; } = new List<OpenRouterModel>();
        }
    }
}