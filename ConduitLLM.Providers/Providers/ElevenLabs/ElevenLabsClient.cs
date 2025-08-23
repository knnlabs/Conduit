using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Providers.Common.Models;
using ConduitLLM.Providers.Translators;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.ElevenLabs
{
    /// <summary>
    /// Client implementation for ElevenLabs voice AI services.
    /// </summary>
    /// <remarks>
    /// ElevenLabs provides high-quality text-to-speech and conversational AI
    /// with support for voice cloning and real-time voice synthesis.
    /// </remarks>
    public class ElevenLabsClient : BaseLLMClient, ILLMClient, ITextToSpeechClient, IRealtimeAudioClient
    {
        private const string DEFAULT_BASE_URL = "https://api.elevenlabs.io/v1";
        
        private readonly ElevenLabsTextToSpeechService _textToSpeechService;
        private readonly ElevenLabsRealtimeService _realtimeService;
        private readonly ElevenLabsVoiceService _voiceService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ElevenLabsClient"/> class.
        /// </summary>
        public ElevenLabsClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string providerModelId,
            ILogger<ElevenLabsClient> logger,
            IHttpClientFactory? httpClientFactory = null,
            ProviderDefaultModels? defaultModels = null)
            : base(provider, keyCredential, providerModelId, logger, httpClientFactory, "ElevenLabs", defaultModels)
        {
            var translatorLogger = logger as ILogger<ElevenLabsRealtimeTranslator>
                ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance.CreateLogger<ElevenLabsRealtimeTranslator>();
            var translator = new ElevenLabsRealtimeTranslator(translatorLogger);
            
            _textToSpeechService = new ElevenLabsTextToSpeechService(logger, DefaultJsonOptions);
            _realtimeService = new ElevenLabsRealtimeService(translator, logger);
            _voiceService = new ElevenLabsVoiceService(logger, DefaultJsonOptions);
        }

        /// <summary>
        /// Sends a chat completion request to ElevenLabs.
        /// </summary>
        public override Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // ElevenLabs is primarily a voice AI provider
            return Task.FromException<ChatCompletionResponse>(
                new NotSupportedException("ElevenLabs does not support text-based chat completion. Use text-to-speech or real-time audio instead."));
        }

        /// <summary>
        /// Streams chat completion responses from ElevenLabs.
        /// </summary>
        public override IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("ElevenLabs does not support streaming text chat. Use text-to-speech or real-time audio instead.");
        }

        /// <summary>
        /// Creates speech audio from text using ElevenLabs.
        /// </summary>
        public async Task<TextToSpeechResponse> CreateSpeechAsync(
            TextToSpeechRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateSpeech");

            var effectiveApiKey = apiKey ?? PrimaryKeyCredential.ApiKey;
            if (string.IsNullOrEmpty(effectiveApiKey))
            {
                throw new InvalidOperationException("API key is required for ElevenLabs");
            }

            using var httpClient = CreateHttpClient(effectiveApiKey);
            var model = request.Model ?? GetDefaultTextToSpeechModel();
            
            return await _textToSpeechService.CreateSpeechAsync(
                httpClient,
                Provider.BaseUrl,
                request,
                model,
                cancellationToken);
        }

        /// <summary>
        /// Streams speech audio from text using ElevenLabs.
        /// </summary>
        public async IAsyncEnumerable<AudioChunk> StreamSpeechAsync(
            TextToSpeechRequest request,
            string? apiKey = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "StreamSpeech");

            var effectiveApiKey = apiKey ?? PrimaryKeyCredential.ApiKey;
            if (string.IsNullOrEmpty(effectiveApiKey))
            {
                throw new InvalidOperationException("API key is required for ElevenLabs");
            }

            using var httpClient = CreateHttpClient(effectiveApiKey);
            var model = request.Model ?? GetDefaultTextToSpeechModel();
            
            await foreach (var chunk in _textToSpeechService.StreamSpeechAsync(
                httpClient,
                Provider.BaseUrl,
                request,
                model,
                cancellationToken))
            {
                yield return chunk;
            }
        }

        /// <summary>
        /// Lists available voices from ElevenLabs.
        /// </summary>
        public async Task<List<VoiceInfo>> ListVoicesAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            var effectiveApiKey = apiKey ?? PrimaryKeyCredential.ApiKey;
            if (string.IsNullOrEmpty(effectiveApiKey))
            {
                throw new InvalidOperationException("API key is required for ElevenLabs");
            }

            using var httpClient = CreateHttpClient(effectiveApiKey);
            
            return await _voiceService.ListVoicesAsync(
                httpClient,
                Provider.BaseUrl,
                cancellationToken);
        }

        /// <summary>
        /// Gets the audio formats supported by ElevenLabs.
        /// </summary>
        public async Task<List<string>> GetSupportedFormatsAsync(
            CancellationToken cancellationToken = default)
        {
            return await _textToSpeechService.GetSupportedFormatsAsync(cancellationToken);
        }

        /// <summary>
        /// Checks if the client supports text-to-speech synthesis.
        /// </summary>
        public async Task<bool> SupportsTextToSpeechAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            return await _textToSpeechService.SupportsTextToSpeechAsync(cancellationToken);
        }

        /// <summary>
        /// Updates the configuration of an active real-time session.
        /// </summary>
        public async Task UpdateSessionAsync(
            RealtimeSession session,
            RealtimeSessionUpdate updates,
            CancellationToken cancellationToken = default)
        {
            await _realtimeService.UpdateSessionAsync(session, updates, cancellationToken);
        }

        /// <summary>
        /// Closes an active real-time session.
        /// </summary>
        public async Task CloseSessionAsync(
            RealtimeSession session,
            CancellationToken cancellationToken = default)
        {
            await _realtimeService.CloseSessionAsync(session, cancellationToken);
        }

        /// <summary>
        /// Checks if the client supports real-time audio conversations.
        /// </summary>
        public async Task<bool> SupportsRealtimeAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            return await _realtimeService.SupportsRealtimeAsync(cancellationToken);
        }

        /// <summary>
        /// Gets the capabilities of the ElevenLabs real-time audio system.
        /// </summary>
        public Task<RealtimeCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
        {
            return _realtimeService.GetCapabilitiesAsync(cancellationToken);
        }

        /// <summary>
        /// Creates a new real-time session with ElevenLabs Conversational AI.
        /// </summary>
        public async Task<RealtimeSession> CreateSessionAsync(
            RealtimeSessionConfig config,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            var effectiveApiKey = apiKey ?? PrimaryKeyCredential.ApiKey;
            if (string.IsNullOrWhiteSpace(effectiveApiKey))
            {
                throw new InvalidOperationException("API key is required for creating a realtime session. Either provide an API key or ensure the client has a valid primary key credential.");
            }
            
            var defaultModel = GetDefaultRealtimeModel();
            
            return await _realtimeService.CreateSessionAsync(
                config,
                effectiveApiKey,
                defaultModel,
                cancellationToken);
        }

        /// <summary>
        /// Streams audio bidirectionally with ElevenLabs.
        /// </summary>
        public IAsyncDuplexStream<RealtimeAudioFrame, RealtimeResponse> StreamAudioAsync(
            RealtimeSession session,
            CancellationToken cancellationToken = default)
        {
            return _realtimeService.StreamAudioAsync(session, cancellationToken);
        }

        /// <summary>
        /// Verifies ElevenLabs authentication by calling the user endpoint.
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
                
                // Use the user endpoint which is free and validates the API key
                var request = new HttpRequestMessage(HttpMethod.Get, "user");
                
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
                    $"ElevenLabs authentication failed: {response.StatusCode}",
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
                Logger.LogError(ex, "Unexpected error during ElevenLabs authentication verification");
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Authentication verification failed: {ex.Message}",
                    ex.ToString());
            }
        }

        /// <summary>
        /// Gets available models from ElevenLabs.
        /// </summary>
        public override async Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.FromResult(new List<ExtendedModelInfo>
            {
                new ExtendedModelInfo
                {
                    Id = "eleven_monolingual_v1",
                    OwnedBy = "elevenlabs",
                    ProviderName = "ElevenLabs",
                    Capabilities = new ConduitLLM.Providers.Common.Models.ModelCapabilities
                    {
                        Chat = false,
                        TextToSpeech = true,
                        RealtimeAudio = false,
                        SupportedAudioOperations = new List<AudioOperation> { AudioOperation.TextToSpeech }
                    }
                },
                new ExtendedModelInfo
                {
                    Id = "eleven_multilingual_v2",
                    OwnedBy = "elevenlabs",
                    ProviderName = "ElevenLabs",
                    Capabilities = new ConduitLLM.Providers.Common.Models.ModelCapabilities
                    {
                        Chat = false,
                        TextToSpeech = true,
                        RealtimeAudio = false,
                        SupportedAudioOperations = new List<AudioOperation> { AudioOperation.TextToSpeech }
                    }
                },
                new ExtendedModelInfo
                {
                    Id = "eleven_conversational_v1",
                    OwnedBy = "elevenlabs",
                    ProviderName = "ElevenLabs",
                    Capabilities = new ConduitLLM.Providers.Common.Models.ModelCapabilities
                    {
                        Chat = false,
                        TextToSpeech = false,
                        RealtimeAudio = true,
                        SupportedAudioOperations = new List<AudioOperation> { AudioOperation.Realtime }
                    }
                }
            });
        }

        /// <summary>
        /// Creates image generation from ElevenLabs.
        /// </summary>
        public override Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException<ImageGenerationResponse>(
                new NotSupportedException("ElevenLabs does not support image generation. Use text-to-speech or real-time audio instead."));
        }

        /// <summary>
        /// Creates embeddings from ElevenLabs.
        /// </summary>
        public override Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException<EmbeddingResponse>(
                new NotSupportedException("ElevenLabs does not support text embeddings. Use text-to-speech or real-time audio instead."));
        }


        #region Configuration Helpers

        /// <summary>
        /// Gets the default text-to-speech model from configuration or falls back to eleven_monolingual_v1.
        /// </summary>
        private string GetDefaultTextToSpeechModel()
        {
            // Check provider-specific override first
            var providerOverride = DefaultModels?.Audio?.ProviderOverrides
                ?.GetValueOrDefault(ProviderName.ToLowerInvariant())?.TextToSpeechModel;

            if (!string.IsNullOrWhiteSpace(providerOverride))
                return providerOverride;

            // Check global default
            var globalDefault = DefaultModels?.Audio?.DefaultTextToSpeechModel;
            if (!string.IsNullOrWhiteSpace(globalDefault))
                return globalDefault;

            // Fallback to hardcoded default for backward compatibility
            return "eleven_monolingual_v1";
        }

        /// <summary>
        /// Gets the default realtime model from configuration or falls back to eleven_conversational_v1.
        /// </summary>
        private string GetDefaultRealtimeModel()
        {
            // Check provider-specific override first
            var providerOverride = DefaultModels?.Realtime?.ProviderOverrides
                ?.GetValueOrDefault(ProviderName.ToLowerInvariant());

            if (!string.IsNullOrWhiteSpace(providerOverride))
                return providerOverride;

            // Check global default
            var globalDefault = DefaultModels?.Realtime?.DefaultRealtimeModel;
            if (!string.IsNullOrWhiteSpace(globalDefault))
                return globalDefault;

            // Fallback to hardcoded default for backward compatibility
            return "eleven_conversational_v1";
        }

        /// <inheritdoc/>
        protected override string GetDefaultBaseUrl()
        {
            return DEFAULT_BASE_URL;
        }

        #endregion
    }
}
