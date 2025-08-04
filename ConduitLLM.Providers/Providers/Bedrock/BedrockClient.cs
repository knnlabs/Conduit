using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

// Removed AWS SDK dependencies - using direct HTTP calls with AWS Signature V4

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Providers.Helpers;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Providers.Bedrock
{
    /// <summary>
    /// Client for interacting with AWS Bedrock API.
    /// </summary>
    public partial class BedrockClient : BaseLLMClient
    {
        private readonly string _region;
        private readonly string _service = "bedrock-runtime";
        
        // JSON serialization options for Bedrock API
        internal static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="BedrockClient"/> class.
        /// </summary>
        /// <param name="credentials">The provider credentials.</param>
        /// <param name="providerModelId">The provider's model identifier.</param>
        /// <param name="logger">The logger to use.</param>
        /// <param name="httpClientFactory">Optional HTTP client factory.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        public BedrockClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string providerModelId,
            ILogger<BedrockClient> logger,
            IHttpClientFactory? httpClientFactory = null,
            ProviderDefaultModels? defaultModels = null)
            : base(
                  provider,
                  keyCredential,
                  providerModelId,
                  logger,
                  httpClientFactory,
                  "bedrock",
                  defaultModels)
        {
            // Extract region from provider.BaseUrl or use default
            // BaseUrl in this case is treated as the AWS region
            _region = string.IsNullOrWhiteSpace(provider.BaseUrl) ? "us-east-1" : provider.BaseUrl;
        }

        /// <inheritdoc />
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");

            // For Bedrock, we don't add the standard Authorization header
            // Instead, we'll handle AWS Signature V4 auth per request
            client.DefaultRequestHeaders.Authorization = null;

            // Set base address using the region
            client.BaseAddress = new Uri($"https://bedrock-runtime.{_region}.amazonaws.com/");
        }
    }
}