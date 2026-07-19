using System.Text.Json;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.MiniMax
{
    /// <summary>
    /// Client for interacting with MiniMax AI APIs.
    /// </summary>
    public partial class MiniMaxClient : BaseLLMClient, IAuthenticationVerifiable
    {
        private const string DefaultBaseUrl = "https://api.minimax.io";
        private readonly string _baseUrl;
        private Func<string, string, int, Task>? _progressCallback;

        /// <summary>
        /// MiniMax chat API returns snake_case properties — needs case-insensitive deserialization.
        /// </summary>
        private static readonly JsonSerializerOptions CaseInsensitiveJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="MiniMaxClient"/> class.
        /// </summary>
        /// <param name="credentials">The provider credentials containing API key and endpoint.</param>
        /// <param name="modelId">The default model ID to use.</param>
        /// <param name="logger">The logger for diagnostic information.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="defaultModels">The default models configuration.</param>
        public MiniMaxClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string modelId,
            ILogger<MiniMaxClient> logger,
            IHttpClientFactory httpClientFactory,
            ProviderDefaultModels? defaultModels = null)
            : base(provider, keyCredential, modelId, logger, httpClientFactory, "minimax", defaultModels)
        {
            _baseUrl = string.IsNullOrWhiteSpace(provider.BaseUrl) ? DefaultBaseUrl : provider.BaseUrl.TrimEnd('/');
            logger.LogInformation("MiniMax client initialized with base URL: {BaseUrl}, Model: {Model}", _baseUrl, modelId);
        }

        /// <inheritdoc />
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            base.ConfigureHttpClient(client, apiKey);
            // Override Accept header for SSE streaming (base sets application/json)
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
            // client.Timeout stays infinite (set by base): all budgets live in the resilience
            // pipeline, and video traffic goes through the dedicated *VideoClient anyway
        }

        /// <summary>
        /// Sets a progress callback for long-running operations like video generation.
        /// </summary>
        /// <param name="callback">The callback function that receives status updates.</param>
        public void SetProgressCallback(Func<string, string, int, Task> callback)
        {
            _progressCallback = callback;
        }

        /// <inheritdoc/>
        protected override string GetDefaultBaseUrl()
        {
            return DefaultBaseUrl;
        }
    }
}