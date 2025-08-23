using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.Common.Models;
using ConduitLLM.Providers.Translators;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Ultravox
{
    /// <summary>
    /// Client implementation for Ultravox real-time voice AI.
    /// </summary>
    /// <remarks>
    /// Ultravox provides low-latency voice AI capabilities optimized for
    /// conversational applications including telephone systems.
    /// </remarks>
    public partial class UltravoxClient : BaseLLMClient, ILLMClient, IRealtimeAudioClient
    {
        private const string DEFAULT_BASE_URL = "https://api.ultravox.ai/v1";
        private const string DEFAULT_WS_BASE_URL = "wss://api.ultravox.ai/v1";
        private readonly IRealtimeMessageTranslator _translator;

        /// <summary>
        /// Initializes a new instance of the <see cref="UltravoxClient"/> class.
        /// </summary>
        public UltravoxClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string providerModelId,
            ILogger<UltravoxClient> logger,
            IHttpClientFactory? httpClientFactory = null,
            ProviderDefaultModels? defaultModels = null)
            : base(provider, keyCredential, providerModelId, logger, httpClientFactory, "Ultravox", defaultModels)
        {
            var translatorLogger = logger as ILogger<UltravoxRealtimeTranslator>
                ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance.CreateLogger<UltravoxRealtimeTranslator>();
            _translator = new UltravoxRealtimeTranslator(translatorLogger);
        }

        /// <summary>
        /// Sends a chat completion request to Ultravox.
        /// </summary>
        public override Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Ultravox is primarily a real-time voice AI provider
            // For text chat, we can use their REST API if available
            return Task.FromException<ChatCompletionResponse>(
                new NotSupportedException("Ultravox does not support text-based chat completion. Use real-time audio instead."));
        }

        /// <summary>
        /// Streams chat completion responses from Ultravox.
        /// </summary>
        public override IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Ultravox does not support streaming text chat. Use real-time audio instead.");
        }


        /// <summary>
        /// Verifies Ultravox authentication by calling the accounts/me endpoint.
        /// This is a free API call that validates the API key.
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
                    return Core.Interfaces.AuthenticationResult.Failure("API key is required");
                }

                using var client = CreateHttpClient(effectiveApiKey);
                
                // Update base URL to the API endpoint
                client.BaseAddress = new Uri("https://api.ultravox.ai/api/");
                
                // Use the accounts/me endpoint which is free and validates the API key
                var request = new HttpRequestMessage(HttpMethod.Get, "accounts/me");
                request.Headers.Remove("Authorization");
                request.Headers.Add("X-API-Key", effectiveApiKey);
                
                var response = await client.SendAsync(request, cancellationToken);
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                if (response.IsSuccessStatusCode)
                {
                    return Core.Interfaces.AuthenticationResult.Success($"Response time: {responseTime:F0}ms");
                }
                
                // Check for specific error codes
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return Core.Interfaces.AuthenticationResult.Failure("Invalid API key");
                }
                
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return Core.Interfaces.AuthenticationResult.Failure("Access denied. Check your API key permissions");
                }
                
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Ultravox authentication failed: {response.StatusCode}",
                    errorContent);
            }
            catch (HttpRequestException ex)
            {
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Network error during authentication: {ex.Message}",
                    ex.ToString());
            }
            catch (TaskCanceledException)
            {
                return Core.Interfaces.AuthenticationResult.Failure("Authentication request timed out");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected error during Ultravox authentication verification");
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Authentication verification failed: {ex.Message}",
                    ex.ToString());
            }
        }

        /// <summary>
        /// Gets available models from Ultravox.
        /// </summary>
        public override async Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Ultravox models are typically accessed via their real-time API
            // Return a static list of known models
            return await Task.FromResult(new List<ExtendedModelInfo>
            {
                new ExtendedModelInfo
                {
                    Id = "ultravox-v1",
                    OwnedBy = "ultravox",
                    ProviderName = "Ultravox",
                    Capabilities = new ConduitLLM.Providers.Common.Models.ModelCapabilities
                    {
                        RealtimeAudio = true,
                        SupportedAudioOperations = new List<AudioOperation> { AudioOperation.Realtime }
                    }
                },
                new ExtendedModelInfo
                {
                    Id = "ultravox-telephony",
                    OwnedBy = "ultravox",
                    ProviderName = "Ultravox",
                    Capabilities = new ConduitLLM.Providers.Common.Models.ModelCapabilities
                    {
                        RealtimeAudio = true,
                        SupportedAudioOperations = new List<AudioOperation> { AudioOperation.Realtime }
                    }
                }
            });
        }

        /// <summary>
        /// Creates an image from Ultravox.
        /// </summary>
        public override Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException<ImageGenerationResponse>(
                new NotSupportedException("Ultravox does not support image generation. Use real-time audio instead."));
        }

        /// <summary>
        /// Creates embeddings from Ultravox.
        /// </summary>
        public override Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException<EmbeddingResponse>(
                new NotSupportedException("Ultravox does not support text embeddings. Use real-time audio instead."));
        }

    }
}
