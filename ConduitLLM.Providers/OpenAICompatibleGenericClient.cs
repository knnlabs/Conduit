using System;
using System.Net.Http;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Client for interacting with generic OpenAI-compatible APIs.
    /// </summary>
    /// <remarks>
    /// This client is designed for services that implement the OpenAI API format
    /// but are not OpenAI itself (e.g., LocalAI, Ollama with OpenAI compatibility,
    /// self-hosted models with OpenAI-compatible interfaces, etc.).
    /// Unlike the standard OpenAI client, this requires an API base URL to be specified.
    /// </remarks>
    public class OpenAICompatibleGenericClient : ConduitLLM.Providers.Providers.OpenAICompatible.OpenAICompatibleClient
    {
        /// <summary>
        /// Initializes a new instance of the OpenAICompatibleGenericClient class.
        /// </summary>
        /// <param name="credentials">LLMProvider credentials containing API key and endpoint configuration.</param>
        /// <param name="providerModelId">The specific model ID to use with this provider.</param>
        /// <param name="logger">Logger for recording diagnostic information.</param>
        /// <param name="httpClientFactory">Factory for creating HttpClient instances with proper configuration.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
        /// <exception cref="ConfigurationException">Thrown when API base URL is missing.</exception>
        public OpenAICompatibleGenericClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string providerModelId,
            ILogger<OpenAICompatibleGenericClient> logger,
            IHttpClientFactory httpClientFactory,
            ProviderDefaultModels? defaultModels = null)
            : base(
                provider,
                keyCredential,
                providerModelId,
                logger,
                httpClientFactory,
                "openai-compatible",
                ValidateAndGetBaseUrl(provider),
                defaultModels)
        {
            // Additional validation is done in ValidateAndGetBaseUrl
        }

        /// <summary>
        /// Validates that the API base URL is provided and returns it.
        /// </summary>
        private static string ValidateAndGetBaseUrl(Provider provider)
        {
            if (string.IsNullOrWhiteSpace(provider.BaseUrl))
            {
                throw new ConfigurationException(
                    "Base URL is required for OpenAI-compatible providers. " +
                    "Please specify the base URL of your OpenAI-compatible API endpoint.");
            }

            // Ensure the URL ends properly (without trailing slash for consistency)
            return provider.BaseUrl.TrimEnd('/');
        }

        /// <summary>
        /// Configures the HTTP client with appropriate headers for generic OpenAI-compatible APIs.
        /// </summary>
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            // Use standard Bearer token authentication like OpenAI
            // Most OpenAI-compatible APIs expect this format
            base.ConfigureHttpClient(client, apiKey);

            // Add a custom user agent to identify requests from Conduit
            client.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM/OpenAI-Compatible");
        }
    }
}
