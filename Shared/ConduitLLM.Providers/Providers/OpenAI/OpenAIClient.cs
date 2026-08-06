using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Providers.Authentication;
using ConduitLLM.Providers.Configuration;

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
    public partial class OpenAIClient : ConduitLLM.Providers.OpenAICompatible.OpenAICompatibleClient
    {
        // API configuration constants
        private static class Constants
        {
            public static class Endpoints
            {
                public const string Models = "/models";
                public const string ChatCompletions = "/chat/completions";
                public const string Embeddings = "/embeddings";
                public const string ImageGenerations = "/images/generations";
            }
        }

        private readonly bool _isAzure;

        /// <summary>
        /// Gets the authentication strategy for the configured provider type.
        /// Azure OpenAI uses the api-key header, standard OpenAI uses a Bearer token.
        /// </summary>
        protected override IAuthenticationStrategy AuthenticationStrategy =>
            ProviderConfigurationRegistry.GetAuthenticationStrategy(Provider.ProviderType);

        /// <summary>
        /// The Azure OpenAI REST API version to send with every request, taken from the provider's
        /// declared <c>api_version</c> setting and falling back to the registry's default.
        /// </summary>
        private string AzureApiVersion =>
            ProviderConfigurationRegistry.GetSettingValue(Provider.ProviderType, Provider.Settings, "api_version")
            ?? ProviderConfigurationRegistry.AzureDefaultApiVersion;

        /// <summary>
        /// Initializes a new instance of the OpenAIClient class.
        /// </summary>
        /// <param name="provider">The provider entity containing configuration.</param>
        /// <param name="primaryKeyCredential">The primary key credential to use for requests.</param>
        /// <param name="providerModelId">The specific model ID to use with this provider. For Azure, this is the deployment name.</param>
        /// <param name="logger">Logger for recording diagnostic information.</param>
        /// <param name="httpClientFactory">Factory for creating HttpClient instances with proper configuration.</param>
        /// <param name="providerName">Optional provider name override. If not specified, uses provider.ProviderName or defaults to "openai".</param>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
        /// <exception cref="ConfigurationException">Thrown when API key is missing for non-Azure providers.</exception>
        public OpenAIClient(
            Provider provider,
            ProviderKeyCredential primaryKeyCredential,
            string providerModelId,
            ILogger<OpenAIClient> logger,
            IHttpClientFactory httpClientFactory,
            string? providerName = null)
            : base(
                provider,
                primaryKeyCredential,
                providerModelId,
                logger,
                httpClientFactory,
                providerName ?? provider.ProviderType.ToString() ?? "openai",
                DetermineBaseUrl(provider, primaryKeyCredential))
        {
            _isAzure = provider.ProviderType == ProviderType.Azure;
        }

        /// <summary>
        /// Determines the effective base URL for the provider and key.
        /// </summary>
        /// <remarks>
        /// A key-level base URL is the narrowest override and wins outright. Otherwise the registry
        /// resolves the provider's base URL, substituting structured settings such as Azure's
        /// <c>{resource_name}</c> - and raising an actionable configuration error when a required one
        /// is missing, rather than letting a malformed URL reach the wire.
        /// </remarks>
        private static string DetermineBaseUrl(Provider provider, ProviderKeyCredential keyCredential)
        {
            if (!string.IsNullOrWhiteSpace(keyCredential.BaseUrl))
            {
                return keyCredential.BaseUrl.TrimEnd('/');
            }

            return ProviderConfigurationRegistry.ResolveBaseUrl(provider);
        }
    }
}
