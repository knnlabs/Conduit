using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.Common.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Fireworks
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
    public class FireworksClient : ConduitLLM.Providers.OpenAICompatible.OpenAICompatibleClient
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
            Provider provider,
            ProviderKeyCredential keyCredential,
            string providerModelId,
            ILogger logger,
            IHttpClientFactory? httpClientFactory = null,
            ProviderDefaultModels? defaultModels = null)
            : base(
                provider,
                keyCredential,
                providerModelId,
                logger,
                httpClientFactory,
                "Fireworks",
                baseUrl: DefaultFireworksBaseUrl,
                defaultModels: defaultModels)
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
        /// Validates credentials for Fireworks.
        /// </summary>
        protected override void ValidateCredentials()
        {
            base.ValidateCredentials();

            // Fireworks requires an API key
            if (string.IsNullOrWhiteSpace(PrimaryKeyCredential.ApiKey))
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

        #region Authentication Verification

        /// <summary>
        /// Verifies Fireworks authentication by making a test request to the models endpoint.
        /// </summary>
        public override async Task<Core.Interfaces.AuthenticationResult> VerifyAuthenticationAsync(
            string? apiKey = null,
            string? baseUrl = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                var effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : PrimaryKeyCredential.ApiKey;
                
                if (string.IsNullOrWhiteSpace(effectiveApiKey))
                {
                    return Core.Interfaces.AuthenticationResult.Failure(
                        "API key is required",
                        "No API key provided for Fireworks authentication");
                }

                // Create a test client
                using var client = CreateHttpClient(effectiveApiKey);
                
                // Make a request to the models endpoint
                var modelsUrl = $"{GetHealthCheckUrl(baseUrl)}/models";
                var response = await client.GetAsync(modelsUrl, cancellationToken);
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                Logger.LogInformation("Fireworks auth check returned status {StatusCode}", response.StatusCode);

                // Check for authentication errors
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return Core.Interfaces.AuthenticationResult.Failure(
                        "Authentication failed",
                        "Invalid API key - Fireworks requires a valid API key");
                }
                
                if (response.IsSuccessStatusCode)
                {
                    return Core.Interfaces.AuthenticationResult.Success(
                        "Connected successfully to Fireworks API",
                        responseTime);
                }

                // Other errors
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Unexpected response: {response.StatusCode}",
                    await response.Content.ReadAsStringAsync(cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error verifying Fireworks authentication");
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Authentication verification failed: {ex.Message}",
                    ex.ToString());
            }
        }

        /// <summary>
        /// Gets the health check URL for Fireworks.
        /// </summary>
        public override string GetHealthCheckUrl(string? baseUrl = null)
        {
            var effectiveBaseUrl = !string.IsNullOrWhiteSpace(baseUrl) 
                ? baseUrl.TrimEnd('/') 
                : (Provider.BaseUrl ?? DefaultFireworksBaseUrl).TrimEnd('/');
            
            return effectiveBaseUrl;
        }

        /// <summary>
        /// Gets the default base URL for Fireworks.
        /// </summary>
        protected override string GetDefaultBaseUrl()
        {
            return DefaultFireworksBaseUrl;
        }

        #endregion
    }
}
