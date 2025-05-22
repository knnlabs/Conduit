using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;

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
    public class OpenAICompatibleGenericClient : OpenAICompatibleClient
    {
        /// <summary>
        /// Initializes a new instance of the OpenAICompatibleGenericClient class.
        /// </summary>
        /// <param name="credentials">Provider credentials containing API key and endpoint configuration.</param>
        /// <param name="providerModelId">The specific model ID to use with this provider.</param>
        /// <param name="logger">Logger for recording diagnostic information.</param>
        /// <param name="httpClientFactory">Factory for creating HttpClient instances with proper configuration.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
        /// <exception cref="ConfigurationException">Thrown when API base URL is missing.</exception>
        public OpenAICompatibleGenericClient(
            ProviderCredentials credentials, 
            string providerModelId, 
            ILogger<OpenAICompatibleGenericClient> logger,
            IHttpClientFactory httpClientFactory)
            : base(
                credentials,
                providerModelId,
                logger,
                httpClientFactory,
                "openai-compatible",
                ValidateAndGetBaseUrl(credentials))
        {
            // Additional validation is done in ValidateAndGetBaseUrl
        }

        /// <summary>
        /// Validates that the API base URL is provided and returns it.
        /// </summary>
        private static string ValidateAndGetBaseUrl(ProviderCredentials credentials)
        {
            if (string.IsNullOrWhiteSpace(credentials.ApiBase))
            {
                throw new ConfigurationException(
                    "API Base URL is required for OpenAI-compatible providers. " +
                    "Please specify the base URL of your OpenAI-compatible API endpoint.");
            }

            // Ensure the URL ends properly (without trailing slash for consistency)
            return credentials.ApiBase.TrimEnd('/');
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