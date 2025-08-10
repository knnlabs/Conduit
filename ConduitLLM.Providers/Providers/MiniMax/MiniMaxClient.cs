using System;
using System.Net.Http;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Providers.Helpers;
using ConduitLLM.Providers.Common.Models;

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
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            client.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");
            // Add Accept header for SSE streaming
            client.DefaultRequestHeaders.Add("Accept", "text/event-stream");
            client.Timeout = TimeSpan.FromMinutes(10); // Long timeout for video processing
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