using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.InternalModels;

using Microsoft.Extensions.Logging;

using CoreModels = ConduitLLM.Core.Models;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Client for interacting with Azure OpenAI Service API.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This client implements the <see cref="ILLMClient"/> interface for Azure OpenAI Service,
    /// which provides a managed API for OpenAI models with Azure enterprise features.
    /// </para>
    /// <para>
    /// It inherits from <see cref="OpenAICompatibleClient"/> to leverage the common OpenAI-compatible
    /// request/response format, but customizes the authentication method, endpoint structure, and
    /// error handling for Azure OpenAI's specific requirements.
    /// </para>
    /// <para>
    /// Key differences from standard OpenAI:
    /// - Uses api-key header instead of Bearer token authentication
    /// - Requires a resource endpoint and deployment name in URL structure
    /// - Includes api-version query parameter in all requests
    /// - Different model listing behavior (deployments instead of models)
    /// </para>
    /// </remarks>
    public class AzureOpenAIClient : OpenAICompatibleClient
    {
        private const string ApiVersion = "2024-02-01";
        private readonly string _deploymentName;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureOpenAIClient"/> class.
        /// </summary>
        /// <param name="credentials">The Azure OpenAI credentials containing API key and resource endpoint.</param>
        /// <param name="deploymentName">The Azure OpenAI deployment name to use (equivalent to model ID in other providers).</param>
        /// <param name="logger">Logger for recording diagnostic information.</param>
        /// <param name="httpClientFactory">Factory for creating HttpClient instances with proper configuration.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
        /// <exception cref="ConfigurationException">Thrown when required Azure-specific configuration is missing.</exception>
        public AzureOpenAIClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string deploymentName,
            ILogger logger,
            IHttpClientFactory? httpClientFactory = null,
            ProviderDefaultModels? defaultModels = null)
            : base(provider ?? throw new ArgumentNullException(nameof(provider)), 
                  keyCredential ?? throw new ArgumentNullException(nameof(keyCredential)),
                  deploymentName, logger, httpClientFactory, 
                  providerName: "azure",
                  baseUrl: null,
                  defaultModels: defaultModels)
        {
            // Deployment name is equivalent to provider model ID in Azure
            _deploymentName = deploymentName ?? throw new ArgumentNullException(nameof(deploymentName), "Deployment name is required for Azure OpenAI.");

            // Validate Azure-specific required fields
            if (string.IsNullOrWhiteSpace(provider.BaseUrl))
            {
                throw new ConfigurationException("BaseUrl (Azure resource endpoint) is required for Azure OpenAI. Format: https://{resource-name}.openai.azure.com");
            }

            // API version is now a constant

            // For Azure OpenAI, we need to override the endpoints using the proper base URL and API version
            // The BaseUrl field is initialized in the base class constructor, we don't need to reassign it here
        }

        /// <summary>
        /// Gets the chat completion endpoint for Azure OpenAI.
        /// </summary>
        /// <returns>The full URL for the chat completions endpoint.</returns>
        protected override string GetChatCompletionEndpoint()
        {
            return $"{BaseUrl}/openai/deployments/{_deploymentName}/chat/completions?api-version={ApiVersion}";
        }

        /// <summary>
        /// Gets the embedding endpoint for Azure OpenAI.
        /// </summary>
        /// <returns>The full URL for the embeddings endpoint.</returns>
        protected override string GetEmbeddingEndpoint()
        {
            return $"{BaseUrl}/openai/deployments/{_deploymentName}/embeddings?api-version={ApiVersion}";
        }

        /// <summary>
        /// Gets the image generation endpoint for Azure OpenAI.
        /// </summary>
        /// <returns>The full URL for the image generations endpoint.</returns>
        protected override string GetImageGenerationEndpoint()
        {
            return $"{BaseUrl}/openai/images/generations?api-version={ApiVersion}";
        }

        /// <summary>
        /// Gets the models endpoint for Azure OpenAI.
        /// </summary>
        /// <returns>The full URL for the models endpoint.</returns>
        /// <remarks>
        /// Azure OpenAI doesn't support listing models via the API key authentication method.
        /// Listing deployments typically requires Azure RBAC permissions and uses Azure management libraries.
        /// This method is overridden to return a valid URL format, but GetModelsAsync will return an empty list.
        /// </remarks>
        protected override string GetModelsEndpoint()
        {
            // Not directly used as Azure doesn't support model listing via API key
            return $"{BaseUrl}/openai/models?api-version={ApiVersion}";
        }

        /// <summary>
        /// Gets available models from Azure OpenAI.
        /// </summary>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A list of available models.</returns>
        /// <remarks>
        /// Azure OpenAI doesn't support listing models via the API key authentication method.
        /// This method returns an empty list to avoid exceptions.
        /// In a real-world scenario, listing Azure OpenAI deployments requires Azure SDK 
        /// and RBAC permissions, which is beyond the scope of simple API key interaction.
        /// </remarks>
        public override Task<List<InternalModels.ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            Logger.LogWarning("Listing models is not supported for Azure OpenAI provider via API key authentication.");
            return Task.FromResult(new List<InternalModels.ExtendedModelInfo>());
        }

        /// <summary>
        /// Configure the HTTP client with Azure OpenAI-specific settings.
        /// </summary>
        /// <param name="client">The HTTP client to configure.</param>
        /// <param name="apiKey">The API key to use for authentication.</param>
        /// <remarks>
        /// Azure OpenAI uses api-key header instead of Bearer token authentication.
        /// </remarks>
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            // Call base but skip the Authorization header setting
            // We'll just configure headers ourselves for Azure

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");

            // Azure OpenAI uses api-key header instead of Authorization header
            client.DefaultRequestHeaders.Add("api-key", apiKey);
        }


        /// <summary>
        /// Gets the capabilities for Azure OpenAI.
        /// </summary>
        /// <param name="modelId">Optional model ID to get capabilities for.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public override Task<CoreModels.ProviderCapabilities> GetCapabilitiesAsync(string? modelId = null)
        {
            var model = modelId ?? ProviderModelId;
            
            // Azure OpenAI supports most OpenAI features plus some additional ones
            return Task.FromResult(new CoreModels.ProviderCapabilities
            {
                Provider = ProviderName,
                ModelId = model,
                ChatParameters = new CoreModels.ChatParameterSupport
                {
                    Temperature = true,
                    MaxTokens = true,
                    TopP = true,
                    TopK = false,
                    Stop = true,
                    PresencePenalty = true,
                    FrequencyPenalty = true,
                    LogitBias = true,
                    N = true,
                    User = true,
                    Seed = true,
                    ResponseFormat = true
                },
                Features = new CoreModels.FeatureSupport
                {
                    Streaming = true,
                    Embeddings = true,
                    ImageGeneration = true,
                    VisionInput = true,
                    FunctionCalling = true,
                    AudioTranscription = true,
                    TextToSpeech = true
                }
            });
        }

        #region Authentication Verification

        /// <summary>
        /// Verifies Azure OpenAI authentication by making a test request to the chat completions endpoint.
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
                        "No API key provided for Azure OpenAI authentication");
                }

                // Create a test client specifically for authentication verification
                using var client = CreateAuthenticationVerificationClient(effectiveApiKey);
                
                // Azure requires the base URL to be set
                var effectiveBaseUrl = !string.IsNullOrWhiteSpace(baseUrl) 
                    ? baseUrl.TrimEnd('/') 
                    : Provider.BaseUrl?.TrimEnd('/');
                    
                if (string.IsNullOrWhiteSpace(effectiveBaseUrl))
                {
                    return Core.Interfaces.AuthenticationResult.Failure(
                        "Base URL is required",
                        "Azure OpenAI requires a resource endpoint URL");
                }
                
                // Make a minimal chat completion request to verify auth
                var testEndpoint = $"{effectiveBaseUrl}/openai/deployments/{_deploymentName}/chat/completions?api-version={ApiVersion}";
                var testRequest = new
                {
                    messages = new[]
                    {
                        new { role = "user", content = "Test" }
                    },
                    max_tokens = 1,
                    temperature = 0
                };

                var json = System.Text.Json.JsonSerializer.Serialize(testRequest);
                var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                var response = await client.PostAsync(testEndpoint, content, cancellationToken);
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                Logger.LogInformation("Azure OpenAI auth check returned status {StatusCode}", response.StatusCode);

                // Check for authentication errors
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return Core.Interfaces.AuthenticationResult.Failure(
                        "Authentication failed",
                        "Invalid API key for Azure OpenAI resource");
                }
                
                // Check for not found (deployment doesn't exist)
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (responseContent.Contains("DeploymentNotFound"))
                    {
                        return Core.Interfaces.AuthenticationResult.Failure(
                            "Deployment not found",
                            $"The deployment '{_deploymentName}' was not found in the Azure OpenAI resource");
                    }
                }
                
                // Success - either 200 OK or rate limit (which means auth succeeded)
                if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    return Core.Interfaces.AuthenticationResult.Success(
                        "Connected successfully to Azure OpenAI",
                        responseTime);
                }

                // Other errors
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Unexpected response: {response.StatusCode}",
                    await response.Content.ReadAsStringAsync(cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error verifying Azure OpenAI authentication");
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Authentication verification failed: {ex.Message}",
                    ex.ToString());
            }
        }

        /// <summary>
        /// Gets the health check URL for Azure OpenAI.
        /// </summary>
        public override string GetHealthCheckUrl(string? baseUrl = null)
        {
            var effectiveBaseUrl = !string.IsNullOrWhiteSpace(baseUrl) 
                ? baseUrl.TrimEnd('/') 
                : Provider.BaseUrl?.TrimEnd('/');
                
            if (string.IsNullOrWhiteSpace(effectiveBaseUrl))
            {
                throw new InvalidOperationException("Azure OpenAI requires a base URL (resource endpoint)");
            }
            
            // Return the base deployments endpoint with API version
            return $"{effectiveBaseUrl}/openai/deployments/{_deploymentName}/chat/completions?api-version={ApiVersion}";
        }

        #endregion
    }
}
