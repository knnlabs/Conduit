using System.Net.Http.Headers;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.DeepInfra
{
    /// <summary>
    /// Client for interacting with DeepInfra's OpenAI-compatible API.
    /// </summary>
    /// <remarks>
    /// <para>
    /// DeepInfra provides a fully OpenAI-compatible API with access to cutting-edge models
    /// including advanced reasoning and coding specialists. This client extends OpenAICompatibleClient 
    /// to provide DeepInfra-specific configuration and behavior.
    /// </para>
    /// <para>
    /// DeepInfra supports multiple model families including Qwen, MoonshotAI, and GLM models
    /// with extensive context windows (up to 262,144 tokens) and multimodal capabilities.
    /// </para>
    /// <para>
    /// Key features:
    /// - Full OpenAI API compatibility for seamless integration
    /// - Support for streaming and non-streaming responses
    /// - Multimodal support (text + image inputs)
    /// - Advanced reasoning and coding models
    /// - Model versioning support (MODEL_NAME:VERSION format)
    /// </para>
    /// </remarks>
    public class DeepInfraClient : ConduitLLM.Providers.OpenAICompatible.OpenAICompatibleClient
    {
        // Default base URL for DeepInfra OpenAI-compatible API
        private const string DefaultDeepInfraBaseUrl = "https://api.deepinfra.com/v1/openai";

        /// <summary>
        /// Initializes a new instance of the <see cref="DeepInfraClient"/> class.
        /// </summary>
        /// <param name="provider">The provider configuration.</param>
        /// <param name="keyCredential">The API key credential.</param>
        /// <param name="providerModelId">The model identifier to use (e.g., Qwen/Qwen3-235B-A22B-Thinking-2507).</param>
        /// <param name="logger">The logger to use.</param>
        /// <param name="httpClientFactory">Optional HTTP client factory for advanced usage scenarios.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        public DeepInfraClient(
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
                "DeepInfra",
                baseUrl: DefaultDeepInfraBaseUrl,
                defaultModels: defaultModels)
        {
        }

        /// <summary>
        /// Configures the HTTP client with DeepInfra-specific settings.
        /// </summary>
        /// <param name="client">The HTTP client to configure.</param>
        /// <param name="apiKey">The API key to use for authentication.</param>
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            // Call base implementation to set standard headers
            base.ConfigureHttpClient(client, apiKey);

            // DeepInfra uses OpenAI-compatible Authentication with Bearer token
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        /// <summary>
        /// Validates credentials for DeepInfra.
        /// </summary>
        protected override void ValidateCredentials()
        {
            base.ValidateCredentials();

            // DeepInfra requires an API key
            if (string.IsNullOrWhiteSpace(PrimaryKeyCredential.ApiKey))
            {
                throw new Core.Exceptions.ConfigurationException($"API key is missing for provider '{ProviderName}'.");
            }
        }

        /// <summary>
        /// Creates embeddings using DeepInfra API.
        /// </summary>
        /// <param name="request">The embedding request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An embedding response.</returns>
        /// <remarks>
        /// DeepInfra supports embeddings through their OpenAI-compatible API.
        /// The model should come from the request or the model mapping system.
        /// </remarks>
        public override async Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Use the base implementation for the actual API call
            return await base.CreateEmbeddingAsync(request, apiKey, cancellationToken);
        }

        /// <summary>
        /// Creates images using DeepInfra API.
        /// </summary>
        /// <param name="request">The image generation request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An image generation response.</returns>
        /// <remarks>
        /// DeepInfra supports image generation through their OpenAI-compatible API.
        /// </remarks>
        public override async Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // DeepInfra supports image generation via OpenAI-compatible endpoint
            return await base.CreateImageAsync(request, apiKey, cancellationToken);
        }

        #region Authentication Verification

        /// <summary>
        /// Verifies DeepInfra authentication by making a test request to the models endpoint.
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
                        "No API key provided for DeepInfra authentication");
                }

                // Create a test client
                using var client = CreateHttpClient(effectiveApiKey);
                
                // Make a request to the models endpoint
                var modelsUrl = $"{GetHealthCheckUrl(baseUrl)}/models";
                var response = await client.GetAsync(modelsUrl, cancellationToken);
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                Logger.LogInformation("DeepInfra auth check returned status {StatusCode}", response.StatusCode);

                // Check for authentication errors
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return Core.Interfaces.AuthenticationResult.Failure(
                        "Authentication failed",
                        "Invalid API key - DeepInfra requires a valid API key");
                }
                
                if (response.IsSuccessStatusCode)
                {
                    return Core.Interfaces.AuthenticationResult.Success(
                        "Connected successfully to DeepInfra API",
                        responseTime);
                }

                // Other errors
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Unexpected response: {response.StatusCode}",
                    await response.Content.ReadAsStringAsync(cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error verifying DeepInfra authentication");
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Authentication verification failed: {ex.Message}",
                    ex.ToString());
            }
        }

        /// <summary>
        /// Gets the health check URL for DeepInfra.
        /// </summary>
        public override string GetHealthCheckUrl(string? baseUrl = null)
        {
            var effectiveBaseUrl = !string.IsNullOrWhiteSpace(baseUrl) 
                ? baseUrl.TrimEnd('/') 
                : (Provider.BaseUrl ?? DefaultDeepInfraBaseUrl).TrimEnd('/');
            
            return effectiveBaseUrl;
        }

        /// <summary>
        /// Gets the default base URL for DeepInfra.
        /// </summary>
        protected override string GetDefaultBaseUrl()
        {
            return DefaultDeepInfraBaseUrl;
        }

        #endregion
    }
}