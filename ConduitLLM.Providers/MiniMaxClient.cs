using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.Helpers;
using ConduitLLM.Providers.InternalModels;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Client for interacting with MiniMax AI APIs.
    /// </summary>
    public class MiniMaxClient : BaseLLMClient
    {
        private const string DefaultBaseUrl = "https://api.minimax.io/v1";
        private readonly string _baseUrl;

        /// <summary>
        /// Initializes a new instance of the <see cref="MiniMaxClient"/> class.
        /// </summary>
        /// <param name="credentials">The provider credentials containing API key and endpoint.</param>
        /// <param name="modelId">The default model ID to use.</param>
        /// <param name="logger">The logger for diagnostic information.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="defaultModels">The default models configuration.</param>
        public MiniMaxClient(
            ProviderCredentials credentials,
            string modelId,
            ILogger<MiniMaxClient> logger,
            IHttpClientFactory httpClientFactory,
            ProviderDefaultModels? defaultModels = null)
            : base(credentials, modelId, logger, httpClientFactory, "minimax", defaultModels)
        {
            _baseUrl = string.IsNullOrWhiteSpace(credentials.ApiBase) ? DefaultBaseUrl : credentials.ApiBase.TrimEnd('/');
        }

        /// <inheritdoc/>
        public override Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // MiniMax doesn't support chat completions yet
            throw new NotSupportedException("MiniMax provider does not support chat completions. Use image generation instead.");
        }

        /// <inheritdoc/>
        public override async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("MiniMax provider does not support chat completions streaming.");
            yield break;
        }

        /// <inheritdoc/>
        public override async Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateImage");

            return await ExecuteApiRequestAsync(async () =>
            {
                using var httpClient = CreateHttpClient(apiKey);
                
                var miniMaxRequest = new MiniMaxImageGenerationRequest
                {
                    Model = request.Model ?? "image-01",
                    Prompt = request.Prompt,
                    AspectRatio = MapSizeToAspectRatio(request.Size),
                    ResponseFormat = "base64", // Always use base64 for consistency
                    N = request.N,
                    PromptOptimizer = true
                };

                // Add subject reference if provided (for future use)
                if (!string.IsNullOrEmpty(request.User))
                {
                    // MiniMax uses this for tracking, not subject reference
                }

                var endpoint = $"{_baseUrl}/image_generation";
                var response = await HttpClientHelper.SendJsonRequestAsync<MiniMaxImageGenerationRequest, MiniMaxImageGenerationResponse>(
                    httpClient, HttpMethod.Post, endpoint, miniMaxRequest, null, null, Logger, cancellationToken);

                // Map MiniMax response to Core response
                var imageData = new List<ImageData>();
                if (response.Data != null)
                {
                    foreach (var item in response.Data)
                    {
                        imageData.Add(new ImageData
                        {
                            Url = item.Url,
                            B64Json = item.B64Json
                        });
                    }
                }

                return new ImageGenerationResponse
                {
                    Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Data = imageData
                };
            }, "CreateImage", cancellationToken);
        }

        /// <inheritdoc/>
        public override Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("MiniMax provider does not support embeddings.");
        }

        /// <inheritdoc/>
        public override async Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // MiniMax doesn't provide a models endpoint, return static list
            return await Task.FromResult(new List<ExtendedModelInfo>
            {
                ExtendedModelInfo.Create("image-01", "minimax", "image-01")
            });
        }

        /// <inheritdoc/>
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            // MiniMax uses a different authentication header
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        private static string MapSizeToAspectRatio(string? size)
        {
            return size switch
            {
                "1792x1024" => "16:9",
                "1024x1792" => "9:16",
                "1024x1024" => "1:1",
                "512x512" => "1:1",
                "2048x2048" => "1:1",
                _ => "1:1" // Default to square
            };
        }

        #region MiniMax-specific Models

        private class MiniMaxImageGenerationRequest
        {
            [System.Text.Json.Serialization.JsonPropertyName("model")]
            public string Model { get; set; } = "image-01";

            [System.Text.Json.Serialization.JsonPropertyName("prompt")]
            public string Prompt { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("aspect_ratio")]
            public string AspectRatio { get; set; } = "1:1";

            [System.Text.Json.Serialization.JsonPropertyName("response_format")]
            public string ResponseFormat { get; set; } = "base64";

            [System.Text.Json.Serialization.JsonPropertyName("n")]
            public int N { get; set; } = 1;

            [System.Text.Json.Serialization.JsonPropertyName("prompt_optimizer")]
            public bool PromptOptimizer { get; set; } = true;

            [System.Text.Json.Serialization.JsonPropertyName("subject_reference")]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public List<object>? SubjectReference { get; set; }
        }

        private class MiniMaxImageGenerationResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("data")]
            public List<MiniMaxImageData>? Data { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("metadata")]
            public object? Metadata { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("base_resp")]
            public BaseResponse? BaseResp { get; set; }
        }

        private class MiniMaxImageData
        {
            [System.Text.Json.Serialization.JsonPropertyName("url")]
            public string? Url { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("b64_json")]
            public string? B64Json { get; set; }
        }

        private class BaseResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("status_code")]
            public int StatusCode { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("status_msg")]
            public string? StatusMsg { get; set; }
        }

        #endregion
    }
}