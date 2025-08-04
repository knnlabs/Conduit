using System.Net.Http;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Providers.Anthropic
{
    /// <summary>
    /// Client for interacting with the Anthropic API to access Claude models for text generation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This client implements the ILLMClient interface for interacting with Anthropic's Claude models.
    /// It supports chat completions with both synchronous and streaming responses.
    /// </para>
    /// <para>
    /// Key features:
    /// - Supports Claude 3 models (Opus, Sonnet, Haiku) and older Claude models
    /// - Handles proper mapping between Conduit's message format and Anthropic's API format
    /// - Supports streaming responses with SSE (Server-Sent Events) parsing
    /// - Includes comprehensive error handling and logging
    /// </para>
    /// <para>
    /// The client automatically handles system messages by moving them to the
    /// dedicated systemPrompt parameter used by Anthropic's API.
    /// </para>
    /// </remarks>
    public partial class AnthropicClient : BaseLLMClient
    {
        // API configuration constants
        private static class Constants
        {
            public static class Urls
            {
                /// <summary>
                /// Default base URL for the Anthropic API
                /// </summary>
                public const string DefaultBaseUrl = "https://api.anthropic.com/v1";
            }

            public static class Headers
            {
                /// <summary>
                /// Required Anthropic API version header value
                /// </summary>
                public const string AnthropicVersion = "2023-06-01";

                /// <summary>
                /// Anthropic API key header name
                /// </summary>
                public const string ApiKeyHeader = "x-api-key";

                /// <summary>
                /// Anthropic API version header name
                /// </summary>
                public const string VersionHeader = "anthropic-version";
            }

            public static class Endpoints
            {
                public const string Messages = "/v1/messages";
            }

            public static class StreamEvents
            {
                /// <summary>
                /// Event type for content block deltas in streaming responses
                /// </summary>
                public const string ContentBlockDelta = "content_block_delta";

                /// <summary>
                /// Event type for message stop in streaming responses
                /// </summary>
                public const string MessageStop = "message_stop";
            }

            public static class ErrorMessages
            {
                public const string MissingApiKey = "API key (x-api-key) is missing for provider 'anthropic'";
                public const string RateLimitExceeded = "Anthropic API rate limit exceeded. Please try again later or reduce your request frequency.";
                public const string InvalidApiKey = "Invalid Anthropic API key. Please check your credentials.";
                public const string ModelNotFound = "Model not found or not available. Available Anthropic models include: claude-3-opus, claude-3-sonnet, claude-3-haiku, claude-2.1, claude-2.0, claude-instant-1.2";
            }
        }

        /// <summary>
        /// Initializes a new instance of the AnthropicClient class.
        /// </summary>
        /// <param name="provider">LLMProvider credentials containing API key and endpoint configuration.</param>
        /// <param name="keyCredential">The key credential to use for authentication.</param>
        /// <param name="providerModelId">The specific Anthropic model ID to use (e.g., claude-3-opus-20240229).</param>
        /// <param name="logger">Logger for recording diagnostic information.</param>
        /// <param name="httpClientFactory">Factory for creating HttpClient instances.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        /// <exception cref="ArgumentNullException">Thrown when credentials, providerModelId, or logger is null.</exception>
        /// <exception cref="ConfigurationException">Thrown when API key is missing in the credentials.</exception>
        public AnthropicClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string providerModelId,
            ILogger<AnthropicClient> logger,
            IHttpClientFactory? httpClientFactory = null,
            ProviderDefaultModels? defaultModels = null)
            : base(
                  provider,
                  keyCredential,
                  providerModelId,
                  logger,
                  httpClientFactory,
                  "anthropic",
                  defaultModels)
        {
        }

        /// <summary>
        /// Validates that the required credentials are present for Anthropic API access.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Anthropic requires an API key for authentication. This method validates that
        /// the API key is present in the credentials before allowing any API calls.
        /// </para>
        /// <para>
        /// The API key should be provided in the ApiKey property of the Providers object.
        /// </para>
        /// </remarks>
        /// <exception cref="ConfigurationException">Thrown when the API key is missing or empty.</exception>
        protected override void ValidateCredentials()
        {
            if (string.IsNullOrWhiteSpace(PrimaryKeyCredential.ApiKey))
            {
                throw new ConfigurationException(Constants.ErrorMessages.MissingApiKey);
            }
        }

        /// <summary>
        /// Configures authentication for the HttpClient.
        /// Anthropic uses custom x-api-key header instead of Bearer tokens.
        /// </summary>
        /// <param name="client">The HttpClient to configure.</param>
        /// <param name="apiKey">The API key to use for authentication.</param>
        protected override void ConfigureAuthentication(HttpClient client, string apiKey)
        {
            // Anthropic uses custom headers instead of Bearer tokens
            // Ensure no Authorization header exists
            client.DefaultRequestHeaders.Authorization = null;
            client.DefaultRequestHeaders.Remove("Authorization");
            
            // Add Anthropic-specific headers
            client.DefaultRequestHeaders.Add(Constants.Headers.ApiKeyHeader, apiKey);
            client.DefaultRequestHeaders.Add(Constants.Headers.VersionHeader, Constants.Headers.AnthropicVersion);
            
            // Do not call base.ConfigureAuthentication() to avoid Bearer tokens
        }
        
        /// <summary>
        /// Configures the HttpClient with necessary headers and settings for Anthropic API requests.
        /// </summary>
        /// <param name="client">The HttpClient to configure.</param>
        /// <param name="apiKey">The API key to use for authentication.</param>
        /// <remarks>
        /// <para>
        /// Anthropic API requires specific headers for authentication and API versioning:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>anthropic-version: The Anthropic API version to use</description></item>
        ///   <item><description>x-api-key: The API key for authentication</description></item>
        ///   <item><description>Accept: application/json for response format</description></item>
        /// </list>
        /// <para>
        /// Unlike many other providers, Anthropic uses a custom x-api-key header instead of
        /// the standard Authorization header with Bearer token. This method removes the
        /// Authorization header set by the base class and adds the custom header.
        /// </para>
        /// <para>
        /// This method also sets the base URL for API requests based on the BaseUrl property
        /// of the Providers, or falls back to the default Anthropic API URL.
        /// </para>
        /// </remarks>
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            // Set the base address if not already set
            var baseUrl = Provider.BaseUrl ?? Constants.Urls.DefaultBaseUrl;
            if (!string.IsNullOrWhiteSpace(baseUrl) && client.BaseAddress == null)
            {
                client.BaseAddress = new System.Uri(baseUrl.TrimEnd('/'));
            }

            // Accept JSON responses
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            // Configure authentication (uses custom headers)
            ConfigureAuthentication(client, apiKey);
        }

        /// <inheritdoc/>
        protected override string GetDefaultBaseUrl()
        {
            return Constants.Urls.DefaultBaseUrl;
        }
    }
}