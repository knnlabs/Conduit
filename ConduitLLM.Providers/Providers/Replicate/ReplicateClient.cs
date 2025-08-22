using System.Net.Http.Headers;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Replicate
{
    /// <summary>
    /// Revised client for interacting with Replicate APIs using the new client hierarchy.
    /// Handles the asynchronous prediction workflow (start, poll, get result) for various model providers.
    /// </summary>
    public partial class ReplicateClient : CustomProviderClient
    {
        // Default base URL for Replicate API
        private const string DefaultReplicateBaseUrl = "https://api.replicate.com/v1/";

        // Default polling configuration
        private static readonly TimeSpan DefaultPollingInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan MaxPollingDuration = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Initializes a new instance of the <see cref="ReplicateClient"/> class.
        /// </summary>
        /// <param name="credentials">The credentials for accessing the Replicate API.</param>
        /// <param name="providerModelId">The model identifier to use (typically a version hash or full slug).</param>
        /// <param name="logger">The logger to use.</param>
        /// <param name="httpClientFactory">The HTTP client factory for creating HttpClient instances.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        public ReplicateClient(
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
                "Replicate",
                baseUrl: DefaultReplicateBaseUrl,
                defaultModels: defaultModels)
        {
        }

        /// <inheritdoc/>
        protected override void ValidateCredentials()
        {
            base.ValidateCredentials();

            if (string.IsNullOrWhiteSpace(PrimaryKeyCredential.ApiKey))
            {
                throw new ConfigurationException($"API key is missing for provider '{ProviderName}'.");
            }
        }

        /// <inheritdoc/>
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            // Customize configuration for Replicate - use Token auth
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", apiKey);

            // Set the base address if not already set
            // Ensure base URL ends with trailing slash for relative path resolution
            if (client.BaseAddress == null && !string.IsNullOrEmpty(BaseUrl))
            {
                var baseUrl = BaseUrl.EndsWith('/') ? BaseUrl : BaseUrl + '/';
                client.BaseAddress = new Uri(baseUrl);
            }
        }

        /// <summary>
        /// Gets the default base URL for Replicate.
        /// </summary>
        protected override string GetDefaultBaseUrl()
        {
            return DefaultReplicateBaseUrl;
        }
    }
}