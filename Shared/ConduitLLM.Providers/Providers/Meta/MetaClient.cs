using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Providers.Common.Models;
using ConduitLLM.Providers.Configuration;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Meta
{
    /// <summary>
    /// Client for interacting with the Meta Model API (Meta AI).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This client interacts with Meta's Model API, which provides access to the Muse Spark
    /// family of multimodal reasoning models from Meta Superintelligence Labs.
    /// </para>
    /// <para>
    /// Key features:
    /// - OpenAI-compatible API format (Chat Completions) for seamless integration
    /// - Support for streaming and non-streaming responses
    /// - 1M-token context window with multimodal input (image, video, PDF)
    /// - Tool calling (including parallel tool calls) and structured output
    /// </para>
    /// <para>
    /// Authentication is handled via Bearer token in the Authorization header.
    /// The API key can be obtained from https://ai.developer.meta.com
    /// </para>
    /// </remarks>
    public partial class MetaClient : ConduitLLM.Providers.OpenAICompatible.OpenAICompatibleClient
    {
        // API configuration constants
        private static class Constants
        {
            public static class Headers
            {
                /// <summary>
                /// Authorization header for API key authentication
                /// </summary>
                public const string Authorization = "Authorization";
            }

            public static class Endpoints
            {
                public const string ChatCompletions = "/chat/completions";
                public const string Models = "/models";
            }

        }

        private static ProviderErrorMessages MetaErrorMessages =>
            ProviderConfigurationRegistry.GetErrorMessages(ProviderType.Meta);

        /// <summary>
        /// Fallback models for Meta when the models endpoint is not available
        /// </summary>
        private static readonly List<ExtendedModelInfo> MetaModels = new()
        {
            // Muse Spark models
            ExtendedModelInfo.Create("muse-spark-1.1", "meta", "Meta Muse Spark 1.1")
        };

        /// <summary>
        /// Initializes a new instance of the MetaClient class.
        /// </summary>
        /// <param name="provider">The provider entity containing configuration.</param>
        /// <param name="keyCredential">Key credential containing API key and endpoint configuration.</param>
        /// <param name="providerModelId">The specific model ID to use with this provider.</param>
        /// <param name="logger">Logger for recording diagnostic information.</param>
        /// <param name="httpClientFactory">Factory for creating HttpClient instances with proper configuration.</param>
        /// <param name="providerName">Optional provider name override. If not specified, defaults to "meta".</param>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
        /// <exception cref="ConfigurationException">Thrown when API key is missing.</exception>
        public MetaClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string providerModelId,
            ILogger<MetaClient> logger,
            IHttpClientFactory httpClientFactory,
            string? providerName = null)
            : base(
                provider,
                keyCredential,
                providerModelId,
                logger,
                httpClientFactory,
                providerName ?? "meta",
                baseUrl: ProviderConfigurationRegistry.ResolveBaseUrl(provider))
        {
            if (string.IsNullOrWhiteSpace(keyCredential.ApiKey))
            {
                throw new ConfigurationException(MetaErrorMessages.MissingApiKey);
            }
        }

        /// <summary>
        /// Configures the HTTP client for Meta Model API requests.
        /// </summary>
        /// <param name="client">The HTTP client to configure.</param>
        /// <param name="apiKey">The API key to configure authentication with.</param>
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            base.ConfigureHttpClient(client, apiKey);

            // Set User-Agent for better API analytics
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ConduitLLM-MetaClient/1.0");
        }
    }
}
