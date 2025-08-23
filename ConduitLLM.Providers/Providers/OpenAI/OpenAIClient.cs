using System.Net.Http.Headers;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.OpenAI
{
    /// <summary>
    /// Client for interacting with OpenAI-compatible APIs, including standard OpenAI,
    /// Azure OpenAI, and other compatible endpoints.
    /// </summary>
    /// <remarks>
    /// This client implements the ILLMClient interface for OpenAI-compatible APIs,
    /// providing a consistent interface for chat completions, embeddings, and image generation.
    /// It supports both OpenAI's standard API endpoint structure and Azure OpenAI's deployment-based
    /// endpoints, with automatic URL and authentication format selection based on the provider name.
    /// </remarks>
    public partial class OpenAIClient : ConduitLLM.Providers.OpenAICompatible.OpenAICompatibleClient,
        Core.Interfaces.IAudioTranscriptionClient,
        Core.Interfaces.ITextToSpeechClient,
        Core.Interfaces.IRealtimeAudioClient
    {
        // Default API configuration constants
        private static class Constants
        {
            public static class Urls
            {
                public const string DefaultOpenAIBaseUrl = "https://api.openai.com/v1";
            }

            // Azure API version is now hardcoded
            public const string AzureApiVersion = "2024-02-01";

            public static class Endpoints
            {
                public const string ChatCompletions = "/chat/completions";
                public const string Models = "/models";
                public const string Embeddings = "/embeddings";
                public const string ImageGenerations = "/images/generations";
                public const string AudioTranscriptions = "/audio/transcriptions";
                public const string AudioTranslations = "/audio/translations";
                public const string AudioSpeech = "/audio/speech";
            }
        }

        private readonly bool _isAzure;
        private readonly IModelCapabilityService? _capabilityService;

        /// <summary>
        /// Initializes a new instance of the OpenAIClient class.
        /// </summary>
        /// <param name="provider">The provider entity containing configuration.</param>
        /// <param name="primaryKeyCredential">The primary key credential to use for requests.</param>
        /// <param name="providerModelId">The specific model ID to use with this provider. For Azure, this is the deployment name.</param>
        /// <param name="logger">Logger for recording diagnostic information.</param>
        /// <param name="httpClientFactory">Factory for creating HttpClient instances with proper configuration.</param>
        /// <param name="capabilityService">Optional service for model capability detection and validation.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        /// <param name="providerName">Optional provider name override. If not specified, uses provider.ProviderName or defaults to "openai".</param>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
        /// <exception cref="ConfigurationException">Thrown when API key is missing for non-Azure providers.</exception>
        public OpenAIClient(
            Provider provider,
            ProviderKeyCredential primaryKeyCredential,
            string providerModelId,
            ILogger<OpenAIClient> logger,
            IHttpClientFactory httpClientFactory,
            IModelCapabilityService? capabilityService = null,
            ProviderDefaultModels? defaultModels = null,
            string? providerName = null)
            : base(
                provider,
                primaryKeyCredential,
                providerModelId,
                logger,
                httpClientFactory,
                providerName ?? provider.ProviderType.ToString() ?? "openai",
                DetermineBaseUrl(provider, primaryKeyCredential, providerName ?? provider.ProviderType.ToString() ?? "openai"),
                defaultModels)
        {
            _isAzure = (providerName ?? provider.ProviderType.ToString() ?? "openai").Equals("azure", StringComparison.OrdinalIgnoreCase);
            _capabilityService = capabilityService;

            // Specific validation for Azure credentials
            if (_isAzure && string.IsNullOrWhiteSpace(provider.BaseUrl) && string.IsNullOrWhiteSpace(primaryKeyCredential.BaseUrl))
            {
                throw new ConfigurationException("BaseUrl (Azure resource endpoint) is required for the 'azure' provider.");
            }
        }

        /// <summary>
        /// Determines the appropriate base URL based on the provider and credentials.
        /// </summary>
        private static string DetermineBaseUrl(Provider provider, ProviderKeyCredential keyCredential, string providerName)
        {
            // Use key credential base URL if specified, otherwise fall back to provider base URL
            var baseUrl = keyCredential.BaseUrl ?? provider.BaseUrl;
            
            // For Azure, we'll handle this specially in the endpoint methods
            if (providerName.Equals("azure", StringComparison.OrdinalIgnoreCase))
            {
                return baseUrl ?? "";
            }

            // For standard OpenAI or compatible providers
            baseUrl = string.IsNullOrWhiteSpace(baseUrl)
                ? Constants.Urls.DefaultOpenAIBaseUrl
                : baseUrl;
            
            // Ensure consistent formatting
            return baseUrl.TrimEnd('/');
        }

        /// <summary>
        /// Configures the HTTP client with appropriate headers and settings.
        /// </summary>
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");

            // Different authentication method for Azure vs. standard OpenAI
            if (_isAzure)
            {
                client.DefaultRequestHeaders.Add("api-key", apiKey);
            }
            else
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }
        }
    }
}