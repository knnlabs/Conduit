using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Providers.InternalModels;

using Microsoft.Extensions.Logging;

using OpenAIModels = ConduitLLM.Providers.InternalModels.OpenAIModels;

namespace ConduitLLM.Providers
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
    public class OpenAIClient : OpenAICompatibleClient,
        Core.Interfaces.IAudioTranscriptionClient,
        Core.Interfaces.ITextToSpeechClient,
        Core.Interfaces.IRealtimeAudioClient
    {
        // Default API configuration constants
        private static class Constants
        {
            public static class Urls
            {
                public const string DefaultOpenAIApiBase = "https://api.openai.com/v1";
            }

            public static class ApiVersions
            {
                public const string DefaultAzureApiVersion = "2024-02-01";
            }

            public static class Endpoints
            {
                public const string ChatCompletions = "/chat/completions";
                public const string Models = "/models";
                public const string Embeddings = "/embeddings";
                public const string ImageGenerations = "/images/generations";
                public const string AudioTranscriptions = "/audio/transcriptions";
                public const string AudioTranslations = "/audio/translations";
                public const string AudioSpeech = "/audio/speech";
            }
        }

        private readonly bool _isAzure;
        private readonly IModelCapabilityService? _capabilityService;

        /// <summary>
        /// Initializes a new instance of the OpenAIClient class.
        /// </summary>
        /// <param name="credentials">Provider credentials containing API key and endpoint configuration.</param>
        /// <param name="providerModelId">The specific model ID to use with this provider. For Azure, this is the deployment name.</param>
        /// <param name="logger">Logger for recording diagnostic information.</param>
        /// <param name="httpClientFactory">Factory for creating HttpClient instances with proper configuration.</param>
        /// <param name="capabilityService">Optional service for model capability detection and validation.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        /// <param name="providerName">Optional provider name override. If not specified, uses credentials.ProviderName or defaults to "openai".</param>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
        /// <exception cref="ConfigurationException">Thrown when API key is missing for non-Azure providers.</exception>
        public OpenAIClient(
            ProviderCredentials credentials,
            string providerModelId,
            ILogger<OpenAIClient> logger,
            IHttpClientFactory httpClientFactory,
            IModelCapabilityService? capabilityService = null,
            ProviderDefaultModels? defaultModels = null,
            string? providerName = null)
            : base(
                credentials,
                providerModelId,
                logger,
                httpClientFactory,
                providerName ?? credentials.ProviderName ?? "openai",
                DetermineBaseUrl(credentials, providerName ?? credentials.ProviderName ?? "openai"),
                defaultModels)
        {
            _isAzure = (providerName ?? credentials.ProviderName ?? "openai").Equals("azure", StringComparison.OrdinalIgnoreCase);
            _capabilityService = capabilityService;

            // Specific validation for Azure credentials
            if (_isAzure && string.IsNullOrWhiteSpace(credentials.ApiBase))
            {
                throw new ConfigurationException("ApiBase (Azure resource endpoint) is required for the 'azure' provider.");
            }
        }

        /// <summary>
        /// Determines the appropriate base URL based on the provider and credentials.
        /// </summary>
        private static string DetermineBaseUrl(ProviderCredentials credentials, string providerName)
        {
            // For Azure, we'll handle this specially in the endpoint methods
            if (providerName.Equals("azure", StringComparison.OrdinalIgnoreCase))
            {
                return credentials.ApiBase ?? "";
            }

            // For standard OpenAI or compatible providers
            return string.IsNullOrWhiteSpace(credentials.ApiBase)
                ? Constants.Urls.DefaultOpenAIApiBase
                : credentials.ApiBase.TrimEnd('/');
        }

        /// <summary>
        /// Configures the HTTP client with appropriate headers and settings.
        /// </summary>
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");

            // Different authentication method for Azure vs. standard OpenAI
            if (_isAzure)
            {
                client.DefaultRequestHeaders.Add("api-key", apiKey);
            }
            else
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }
        }

        /// <summary>
        /// Gets the chat completion endpoint, which differs between Azure and standard OpenAI.
        /// </summary>
        protected override string GetChatCompletionEndpoint()
        {
            if (_isAzure)
            {
                string apiVersion = !string.IsNullOrWhiteSpace(Credentials.ApiVersion)
                    ? Credentials.ApiVersion
                    : Constants.ApiVersions.DefaultAzureApiVersion;

                return $"{BaseUrl.TrimEnd('/')}/openai/deployments/{ProviderModelId}/chat/completions?api-version={apiVersion}";
            }

            return $"{BaseUrl}{Constants.Endpoints.ChatCompletions}";
        }

        /// <summary>
        /// Gets the models endpoint, which differs between Azure and standard OpenAI.
        /// </summary>
        protected override string GetModelsEndpoint()
        {
            if (_isAzure)
            {
                string apiVersion = !string.IsNullOrWhiteSpace(Credentials.ApiVersion)
                    ? Credentials.ApiVersion
                    : Constants.ApiVersions.DefaultAzureApiVersion;

                return $"{BaseUrl.TrimEnd('/')}/openai/deployments?api-version={apiVersion}";
            }

            return $"{BaseUrl}{Constants.Endpoints.Models}";
        }

        /// <summary>
        /// Gets the embedding endpoint, which differs between Azure and standard OpenAI.
        /// </summary>
        protected override string GetEmbeddingEndpoint()
        {
            if (_isAzure)
            {
                string apiVersion = !string.IsNullOrWhiteSpace(Credentials.ApiVersion)
                    ? Credentials.ApiVersion
                    : Constants.ApiVersions.DefaultAzureApiVersion;

                return $"{BaseUrl.TrimEnd('/')}/openai/deployments/{ProviderModelId}/embeddings?api-version={apiVersion}";
            }

            return $"{BaseUrl}{Constants.Endpoints.Embeddings}";
        }

        /// <summary>
        /// Gets the image generation endpoint, which differs between Azure and standard OpenAI.
        /// </summary>
        protected override string GetImageGenerationEndpoint()
        {
            if (_isAzure)
            {
                string apiVersion = !string.IsNullOrWhiteSpace(Credentials.ApiVersion)
                    ? Credentials.ApiVersion
                    : Constants.ApiVersions.DefaultAzureApiVersion;

                return $"{BaseUrl.TrimEnd('/')}/openai/deployments/{ProviderModelId}/images/generations?api-version={apiVersion}";
            }

            return $"{BaseUrl}{Constants.Endpoints.ImageGenerations}";
        }

        #region Audio API Implementation

        /// <summary>
        /// Transcribes audio content into text using OpenAI's Whisper model.
        /// </summary>
        public async Task<AudioTranscriptionResponse> TranscribeAudioAsync(
            AudioTranscriptionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "TranscribeAudio");

            using var client = CreateHttpClient(apiKey);

            var endpoint = _isAzure
                ? GetAzureAudioEndpoint("transcriptions")
                : $"{BaseUrl}{Constants.Endpoints.AudioTranscriptions}";

            using var content = new MultipartFormDataContent();

            // Add audio file
            if (request.AudioData != null)
            {
                var audioContent = new ByteArrayContent(request.AudioData);
                audioContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Add(audioContent, "file", request.FileName ?? "audio.mp3");
            }
            else if (!string.IsNullOrWhiteSpace(request.AudioUrl))
            {
                throw new NotSupportedException("URL-based audio transcription is not supported by OpenAI API. Please provide audio data directly.");
            }

            // Add model - use configuration or fallback to whisper-1
            var defaultTranscriptionModel = GetDefaultTranscriptionModel();
            content.Add(new StringContent(request.Model ?? defaultTranscriptionModel), "model");

            // Add optional parameters
            if (!string.IsNullOrWhiteSpace(request.Language))
                content.Add(new StringContent(request.Language), "language");

            if (!string.IsNullOrWhiteSpace(request.Prompt))
                content.Add(new StringContent(request.Prompt), "prompt");

            if (request.Temperature.HasValue)
                content.Add(new StringContent(request.Temperature.Value.ToString()), "temperature");

            if (request.ResponseFormat.HasValue)
                content.Add(new StringContent(request.ResponseFormat.Value.ToString().ToLowerInvariant()), "response_format");

            return await ExecuteApiRequestAsync(async () =>
            {
                var response = await client.PostAsync(endpoint, content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await ReadErrorContentAsync(response, cancellationToken);
                    throw new LLMCommunicationException(
                        $"Audio transcription failed: {error}",
                        response.StatusCode,
                        ProviderName);
                }

                var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

                // Handle different response formats
                if (request.ResponseFormat == TranscriptionFormat.Text ||
                    request.ResponseFormat == TranscriptionFormat.Srt ||
                    request.ResponseFormat == TranscriptionFormat.Vtt)
                {
                    return new AudioTranscriptionResponse
                    {
                        Text = responseText,
                        Model = request.Model ?? GetDefaultTranscriptionModel()
                    };
                }

                // Default JSON response
                var jsonResponse = JsonSerializer.Deserialize<OpenAIModels.TranscriptionResponse>(responseText, DefaultJsonOptions);

                return new AudioTranscriptionResponse
                {
                    Text = jsonResponse?.Text ?? string.Empty,
                    Language = jsonResponse?.Language,
                    Duration = jsonResponse?.Duration,
                    Model = request.Model ?? "whisper-1",
                    Segments = jsonResponse?.Segments?.Select(s => new TranscriptionSegment
                    {
                        Id = s.Id,
                        Start = s.Start,
                        End = s.End,
                        Text = s.Text
                    }).ToList(),
                    Words = jsonResponse?.Words?.Select(w => new TranscriptionWord
                    {
                        Word = w.Word,
                        Start = w.Start,
                        End = w.End
                    }).ToList()
                };
            }, "TranscribeAudio", cancellationToken);
        }

        /// <summary>
        /// Converts text into speech using OpenAI's TTS models.
        /// </summary>
        public async Task<TextToSpeechResponse> CreateSpeechAsync(
            TextToSpeechRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateSpeech");

            using var client = CreateHttpClient(apiKey);

            var endpoint = _isAzure
                ? GetAzureAudioEndpoint("speech")
                : $"{BaseUrl}{Constants.Endpoints.AudioSpeech}";

            var openAIRequest = new OpenAIModels.TextToSpeechRequest
            {
                Model = request.Model ?? GetDefaultTextToSpeechModel(),
                Input = request.Input,
                Voice = request.Voice,
                ResponseFormat = MapAudioFormat(request.ResponseFormat),
                Speed = request.Speed
            };

            var json = JsonSerializer.Serialize(openAIRequest, DefaultJsonOptions);
            using var content = new StringContent(json, Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            return await ExecuteApiRequestAsync(async () =>
            {
                var response = await client.PostAsync(endpoint, content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await ReadErrorContentAsync(response, cancellationToken);
                    throw new LLMCommunicationException(
                        $"Text-to-speech failed: {error}",
                        response.StatusCode,
                        ProviderName);
                }

                var audioData = await response.Content.ReadAsByteArrayAsync(cancellationToken);

                return new TextToSpeechResponse
                {
                    AudioData = audioData,
                    Format = request.ResponseFormat?.ToString().ToLowerInvariant() ?? "mp3",
                    VoiceUsed = request.Voice,
                    ModelUsed = request.Model ?? GetDefaultTextToSpeechModel(),
                    CharacterCount = request.Input.Length
                };
            }, "CreateSpeech", cancellationToken);
        }

        /// <summary>
        /// Streams text-to-speech audio as it's generated.
        /// </summary>
        public async IAsyncEnumerable<AudioChunk> StreamSpeechAsync(
            TextToSpeechRequest request,
            string? apiKey = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // OpenAI API doesn't support streaming TTS yet, so we'll get the full response and chunk it
            var response = await CreateSpeechAsync(request, apiKey, cancellationToken);

            // Simulate streaming by chunking the response
            const int chunkSize = 4096; // 4KB chunks
            var totalChunks = (int)Math.Ceiling((double)response.AudioData.Length / chunkSize);

            for (int i = 0; i < totalChunks; i++)
            {
                var offset = i * chunkSize;
                var length = Math.Min(chunkSize, response.AudioData.Length - offset);
                var chunkData = new byte[length];
                Array.Copy(response.AudioData, offset, chunkData, 0, length);

                yield return new AudioChunk
                {
                    Data = chunkData,
                    ChunkIndex = i,
                    IsFinal = i == totalChunks - 1
                };

                // Small delay to simulate streaming
                await Task.Delay(10, cancellationToken);
            }
        }

        /// <summary>
        /// Lists available voices for text-to-speech.
        /// </summary>
        public async Task<List<VoiceInfo>> ListVoicesAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // OpenAI has a fixed set of voices, return them directly
            await Task.CompletedTask; // Async method signature requirement

            return new List<VoiceInfo>
            {
                new VoiceInfo
                {
                    VoiceId = "alloy",
                    Name = "Alloy",
                    Description = "Neutral and balanced voice",
                    Gender = VoiceGender.Neutral
                },
                new VoiceInfo
                {
                    VoiceId = "echo",
                    Name = "Echo",
                    Description = "Smooth male voice",
                    Gender = VoiceGender.Male
                },
                new VoiceInfo
                {
                    VoiceId = "fable",
                    Name = "Fable",
                    Description = "Expressive British voice",
                    Gender = VoiceGender.Male,
                    Accent = "British"
                },
                new VoiceInfo
                {
                    VoiceId = "onyx",
                    Name = "Onyx",
                    Description = "Deep male voice",
                    Gender = VoiceGender.Male
                },
                new VoiceInfo
                {
                    VoiceId = "nova",
                    Name = "Nova",
                    Description = "Friendly female voice",
                    Gender = VoiceGender.Female
                },
                new VoiceInfo
                {
                    VoiceId = "shimmer",
                    Name = "Shimmer",
                    Description = "Warm female voice",
                    Gender = VoiceGender.Female
                }
            };
        }

        /// <summary>
        /// Checks if the client supports audio transcription.
        /// </summary>
        public async Task<bool> SupportsTranscriptionAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            if (_capabilityService != null)
            {
                try
                {
                    return await _capabilityService.SupportsAudioTranscriptionAsync(ProviderModelId);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to check transcription capability via ModelCapabilityService, falling back to default");
                }
            }
            
            // Fallback: OpenAI generally supports transcription with Whisper models
            return true;
        }

        /// <summary>
        /// Gets supported audio formats for transcription.
        /// </summary>
        public async Task<List<string>> GetSupportedFormatsAsync(
            CancellationToken cancellationToken = default)
        {
            if (_capabilityService != null)
            {
                try
                {
                    return await _capabilityService.GetSupportedFormatsAsync(ProviderModelId);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to get supported formats via ModelCapabilityService, falling back to default");
                }
            }
            
            // Fallback: OpenAI Whisper supported formats
            return new List<string> { "mp3", "mp4", "mpeg", "mpga", "m4a", "wav", "webm" };
        }

        /// <summary>
        /// Gets supported languages for transcription.
        /// </summary>
        public async Task<List<string>> GetSupportedLanguagesAsync(
            CancellationToken cancellationToken = default)
        {
            if (_capabilityService != null)
            {
                try
                {
                    return await _capabilityService.GetSupportedLanguagesAsync(ProviderModelId);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to get supported languages via ModelCapabilityService, falling back to default");
                }
            }
            
            // Fallback: Whisper supports many languages
            return new List<string>
            {
                "en", "zh", "de", "es", "ru", "ko", "fr", "ja", "pt", "tr",
                "pl", "ca", "nl", "ar", "sv", "it", "id", "hi", "fi", "vi",
                "he", "uk", "el", "ms", "cs", "ro", "da", "hu", "ta", "no",
                "th", "ur", "hr", "bg", "lt", "la", "mi", "ml", "cy", "sk",
                "te", "fa", "lv", "bn", "sr", "az", "sl", "kn", "et", "mk",
                "br", "eu", "is", "hy", "ne", "mn", "bs", "kk", "sq", "sw",
                "gl", "mr", "pa", "si", "km", "sn", "yo", "so", "af", "oc",
                "ka", "be", "tg", "sd", "gu", "am", "yi", "lo", "uz", "fo",
                "ht", "ps", "tk", "nn", "mt", "sa", "lb", "my", "bo", "tl",
                "mg", "as", "tt", "haw", "ln", "ha", "ba", "jw", "su"
            };
        }

        /// <summary>
        /// Checks if the client supports text-to-speech.
        /// </summary>
        public async Task<bool> SupportsTextToSpeechAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            if (_capabilityService != null)
            {
                try
                {
                    return await _capabilityService.SupportsTextToSpeechAsync(ProviderModelId);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to check TTS capability via ModelCapabilityService, falling back to default");
                }
            }
            
            // Fallback: OpenAI generally supports TTS
            return true;
        }

        /// <summary>
        /// Gets supported audio formats for text-to-speech.
        /// </summary>
        async Task<List<string>> Core.Interfaces.ITextToSpeechClient.GetSupportedFormatsAsync(
            CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return new List<string> { "mp3", "opus", "aac", "flac", "wav", "pcm" };
        }

        /// <summary>
        /// Gets the Azure-specific audio endpoint.
        /// </summary>
        private string GetAzureAudioEndpoint(string operation)
        {
            string apiVersion = !string.IsNullOrWhiteSpace(Credentials.ApiVersion)
                ? Credentials.ApiVersion
                : Constants.ApiVersions.DefaultAzureApiVersion;

            return $"{BaseUrl.TrimEnd('/')}/openai/deployments/{ProviderModelId}/audio/{operation}?api-version={apiVersion}";
        }

        /// <summary>
        /// Maps the audio format enum to OpenAI's expected string format.
        /// </summary>
        private static string? MapAudioFormat(AudioFormat? format)
        {
            if (!format.HasValue) return null;

            return format.Value switch
            {
                AudioFormat.Mp3 => "mp3",
                AudioFormat.Opus => "opus",
                AudioFormat.Aac => "aac",
                AudioFormat.Flac => "flac",
                AudioFormat.Wav => "wav",
                AudioFormat.Pcm => "pcm",
                _ => "mp3"
            };
        }

        #endregion

        /// <summary>
        /// Maps the Azure OpenAI response format to the standard models list.
        /// </summary>
        public override async Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            if (_isAzure)
            {
                // Azure has a different response format for listing models
                return await ExecuteApiRequestAsync(async () =>
                {
                    using var client = CreateHttpClient(apiKey);
                    var endpoint = GetModelsEndpoint();

                    var response = await ConduitLLM.Core.Utilities.HttpClientHelper.SendJsonRequestAsync<object, AzureOpenAIModels.ListDeploymentsResponse>(
                        client,
                        HttpMethod.Get,
                        endpoint,
                        // Use empty string instead of null to avoid possible null reference
                        string.Empty,
                        new Dictionary<string, string>(),
                        DefaultJsonOptions,
                        Logger,
                        cancellationToken);

                    return response.Data
                        .Select(m =>
                        {
                            var model = ExtendedModelInfo.Create(m.DeploymentId, ProviderName, m.DeploymentId)
                                .WithName(m.Model ?? m.DeploymentId)
                                .WithCapabilities(new InternalModels.ModelCapabilities
                                {
                                    Chat = true,
                                    TextGeneration = true
                                });

                            // Can't add custom properties directly, but they'll be ignored anyway
                            return model;
                        })
                        .ToList();
                }, "GetModels", cancellationToken);
            }

            // Use the base implementation for standard OpenAI
            return await base.GetModelsAsync(apiKey, cancellationToken);
        }


        #region IRealtimeAudioClient Implementation

        public async Task<RealtimeSession> ConnectAsync(
            string? apiKey,
            RealtimeSessionConfig config,
            CancellationToken cancellationToken = default)
        {
            // OpenAI Realtime API uses WebSocket connection
            var wsUrl = BaseUrl.Replace("https://", "wss://").Replace("http://", "ws://");
            var defaultRealtimeModel = GetDefaultRealtimeModel();
            wsUrl = $"{wsUrl}/realtime?model={config.Model ?? defaultRealtimeModel}";

            var effectiveApiKey = apiKey ?? Credentials.ApiKey ?? throw new InvalidOperationException("API key is required");
            var session = new OpenAIRealtimeSession(wsUrl, effectiveApiKey, config, Logger);
            await session.ConnectAsync(cancellationToken);

            return session;
        }

        public async Task<bool> SupportsRealtimeAsync(string model, CancellationToken cancellationToken = default)
        {
            if (_capabilityService != null)
            {
                try
                {
                    return await _capabilityService.SupportsRealtimeAudioAsync(model);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to check realtime capability via ModelCapabilityService, falling back to default");
                }
            }
            
            // Fallback: Check against known OpenAI realtime models
            var supportedModels = new[] { "gpt-4o-realtime-preview", "gpt-4o-realtime-preview-2024-10-01" };
            return supportedModels.Contains(model);
        }

        public Task<RealtimeCapabilities> GetRealtimeCapabilitiesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RealtimeCapabilities
            {
                SupportedInputFormats = new List<RealtimeAudioFormat>
                {
                    RealtimeAudioFormat.PCM16_24kHz,
                    RealtimeAudioFormat.PCM16_16kHz,
                    RealtimeAudioFormat.G711_ULAW,
                    RealtimeAudioFormat.G711_ALAW
                },
                SupportedOutputFormats = new List<RealtimeAudioFormat>
                {
                    RealtimeAudioFormat.PCM16_24kHz,
                    RealtimeAudioFormat.PCM16_16kHz
                },
                AvailableVoices = new List<VoiceInfo>
                {
                    new VoiceInfo { VoiceId = "alloy", Name = "Alloy", Gender = VoiceGender.Neutral },
                    new VoiceInfo { VoiceId = "echo", Name = "Echo", Gender = VoiceGender.Male },
                    new VoiceInfo { VoiceId = "shimmer", Name = "Shimmer", Gender = VoiceGender.Female }
                },
                SupportedLanguages = new List<string> { "en", "es", "fr", "de", "it", "pt", "ru", "zh", "ja", "ko" },
                SupportsFunctionCalling = true,
                SupportsInterruptions = true
            });
        }

        #endregion

        #region IRealtimeAudioClient Implementation

        /// <inheritdoc />
        public async Task<RealtimeSession> CreateSessionAsync(
            RealtimeSessionConfig config,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            return await ConnectAsync(apiKey, config, cancellationToken);
        }

        /// <inheritdoc />
        public Core.Interfaces.IAsyncDuplexStream<RealtimeAudioFrame, RealtimeResponse> StreamAudioAsync(
            RealtimeSession session,
            CancellationToken cancellationToken = default)
        {
            if (session is not OpenAIRealtimeSession openAISession)
                throw new InvalidOperationException("Session must be created by this client");

            return new OpenAIRealtimeStream(openAISession, Logger as ILogger<OpenAIClient> ??
                throw new InvalidOperationException("Logger must be ILogger<OpenAIClient>"));
        }

        /// <inheritdoc />
        public async Task UpdateSessionAsync(
            RealtimeSession session,
            RealtimeSessionUpdate updates,
            CancellationToken cancellationToken = default)
        {
            if (session is not OpenAIRealtimeSession openAISession)
                throw new InvalidOperationException("Session must be created by this client");

            // For OpenAI, we need to create a provider-specific message
            var providerMessage = new Dictionary<string, object>
            {
                ["type"] = "session.update",
                ["session"] = new Dictionary<string, object?>()
            };

            var sessionData = (Dictionary<string, object?>)providerMessage["session"];

            if (updates.SystemPrompt != null)
                sessionData["instructions"] = updates.SystemPrompt;

            if (updates.Temperature.HasValue)
                sessionData["temperature"] = updates.Temperature.Value;

            if (updates.VoiceSettings != null && updates.VoiceSettings.Speed.HasValue)
                sessionData["speed"] = updates.VoiceSettings.Speed.Value;

            if (updates.TurnDetection != null)
            {
                sessionData["turn_detection"] = new Dictionary<string, object>
                {
                    ["type"] = updates.TurnDetection.Type.ToString().ToLowerInvariant(),
                    ["threshold"] = updates.TurnDetection.Threshold ?? 0.5,
                    ["prefix_padding_ms"] = updates.TurnDetection.PrefixPaddingMs ?? 300,
                    ["silence_duration_ms"] = updates.TurnDetection.SilenceThresholdMs ?? 500
                };
            }

            if (updates.Tools != null)
            {
                sessionData["tools"] = updates.Tools.Select(t => new
                {
                    type = "function",
                    function = new
                    {
                        name = t.Function?.Name,
                        description = t.Function?.Description,
                        parameters = t.Function?.Parameters
                    }
                }).ToList();
            }

            // Convert to JSON and send as a raw message
            var json = JsonSerializer.Serialize(providerMessage, DefaultJsonOptions);
            await openAISession.SendRawMessageAsync(json, cancellationToken);
        }

        /// <inheritdoc />
        public async Task CloseSessionAsync(
            RealtimeSession session,
            CancellationToken cancellationToken = default)
        {
            session?.Dispose();
            await Task.CompletedTask;
        }

        /// <inheritdoc />
        Task<bool> Core.Interfaces.IRealtimeAudioClient.SupportsRealtimeAsync(string? apiKey, CancellationToken cancellationToken)
        {
            // OpenAI supports real-time with appropriate models
            return Task.FromResult(true);
        }

        /// <inheritdoc />
        Task<RealtimeCapabilities> Core.Interfaces.IRealtimeAudioClient.GetCapabilitiesAsync(CancellationToken cancellationToken)
        {
            return GetRealtimeCapabilitiesAsync(cancellationToken);
        }

        /// <summary>
        /// OpenAI-specific realtime stream implementation.
        /// </summary>
        private class OpenAIRealtimeStream : Core.Interfaces.IAsyncDuplexStream<RealtimeAudioFrame, RealtimeResponse>
        {
            private readonly OpenAIRealtimeSession _session;
            private readonly ILogger<OpenAIClient> _logger;

            public OpenAIRealtimeStream(OpenAIRealtimeSession session, ILogger<OpenAIClient> logger)
            {
                _session = session;
                _logger = logger;
            }

            public bool IsConnected => _session.State == SessionState.Connected || _session.State == SessionState.Active;

            public async ValueTask SendAsync(RealtimeAudioFrame item, CancellationToken cancellationToken = default)
            {
                if (item.AudioData != null && item.AudioData.Length > 0)
                {
                    // For OpenAI, we need to send the raw provider-specific message
                    var providerMessage = new Dictionary<string, object>
                    {
                        ["type"] = "input_audio_buffer.append",
                        ["audio"] = Convert.ToBase64String(item.AudioData)
                    };

                    var json = JsonSerializer.Serialize(providerMessage, DefaultJsonOptions);
                    await _session.SendRawMessageAsync(json, cancellationToken);
                }
            }

            public async IAsyncEnumerable<RealtimeResponse> ReceiveAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                await foreach (var message in _session.ReceiveMessagesAsync(cancellationToken))
                {
                    var response = ConvertToRealtimeResponse(message);
                    if (response != null)
                        yield return response;
                }
            }

            public async ValueTask CompleteAsync()
            {
                var providerMessage = new Dictionary<string, object>
                {
                    ["type"] = "input_audio_buffer.commit"
                };

                var json = JsonSerializer.Serialize(providerMessage, DefaultJsonOptions);
                await _session.SendRawMessageAsync(json, CancellationToken.None);
            }

            private RealtimeResponse? ConvertToRealtimeResponse(RealtimeMessage message)
            {
                // The translator should have already converted to RealtimeResponse
                if (message is RealtimeResponse response)
                    return response;

                // If not, we have an unexpected message type
                _logger.LogWarning("Received unexpected message type: {Type}", message.GetType().Name);
                return null;
            }
        }

        #endregion

        #region Capabilities

        /// <inheritdoc />
        public override Task<ProviderCapabilities> GetCapabilitiesAsync(string? modelId = null)
        {
            var model = modelId ?? ProviderModelId;
            var isGpt4Vision = model.Contains("vision", StringComparison.OrdinalIgnoreCase) || 
                               model.Contains("gpt-4o", StringComparison.OrdinalIgnoreCase) ||
                               model.Contains("gpt-4-turbo", StringComparison.OrdinalIgnoreCase);
            var isDalleModel = model.StartsWith("dall-e", StringComparison.OrdinalIgnoreCase);
            var isEmbeddingModel = model.Contains("embedding", StringComparison.OrdinalIgnoreCase) ||
                                   model.Contains("ada", StringComparison.OrdinalIgnoreCase);

            return Task.FromResult(new ProviderCapabilities
            {
                Provider = ProviderName,
                ModelId = model,
                ChatParameters = new ChatParameterSupport
                {
                    Temperature = true,
                    MaxTokens = true,
                    TopP = true,
                    TopK = false, // OpenAI doesn't support top-k
                    Stop = true,
                    PresencePenalty = true,
                    FrequencyPenalty = true,
                    LogitBias = true,
                    N = true,
                    User = true,
                    Seed = true,
                    ResponseFormat = true,
                    Tools = !isDalleModel && !isEmbeddingModel, // Most models support tools except DALL-E and embedding models
                    Constraints = new ParameterConstraints
                    {
                        TemperatureRange = new Range<double>(0.0, 2.0),
                        TopPRange = new Range<double>(0.0, 1.0),
                        MaxStopSequences = 4,
                        MaxTokenLimit = GetModelMaxTokens(model)
                    }
                },
                Features = new FeatureSupport
                {
                    Streaming = !isDalleModel && !isEmbeddingModel,
                    Embeddings = isEmbeddingModel,
                    ImageGeneration = isDalleModel,
                    VisionInput = isGpt4Vision,
                    FunctionCalling = !isDalleModel && !isEmbeddingModel,
                    AudioTranscription = !isDalleModel && !isEmbeddingModel,
                    TextToSpeech = !isDalleModel && !isEmbeddingModel
                }
            });
        }

        private int GetModelMaxTokens(string model)
        {
            return model.ToLowerInvariant() switch
            {
                var m when m.Contains("gpt-4o") => 128000,
                var m when m.Contains("gpt-4-turbo") => 128000,
                var m when m.Contains("gpt-4") => 8192,
                var m when m.Contains("gpt-3.5-turbo") => 16385,
                var m when m.Contains("text-davinci") => 4097,
                var m when m.Contains("text-curie") => 2049,
                var m when m.Contains("text-babbage") => 2049,
                var m when m.Contains("text-ada") => 2049,
                _ => 4096 // Default fallback
            };
        }

        #endregion

        #region Configuration Helpers

        /// <summary>
        /// Gets the default transcription model from configuration or falls back to whisper-1.
        /// </summary>
        private string GetDefaultTranscriptionModel()
        {
            // Check provider-specific override first
            var providerOverride = DefaultModels?.Audio?.ProviderOverrides
                ?.GetValueOrDefault(ProviderName.ToLowerInvariant())?.TranscriptionModel;

            if (!string.IsNullOrWhiteSpace(providerOverride))
                return providerOverride;

            // Check global default
            var globalDefault = DefaultModels?.Audio?.DefaultTranscriptionModel;
            if (!string.IsNullOrWhiteSpace(globalDefault))
                return globalDefault;

            // Use ModelCapabilityService if available
            if (_capabilityService != null)
            {
                try
                {
                    var defaultModel = _capabilityService.GetDefaultModelAsync("openai", "transcription").GetAwaiter().GetResult();
                    if (!string.IsNullOrWhiteSpace(defaultModel))
                        return defaultModel;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to get default transcription model via ModelCapabilityService");
                }
            }

            // Fallback to hardcoded default for backward compatibility
            return "whisper-1";
        }

        /// <summary>
        /// Gets the default text-to-speech model from configuration or falls back to tts-1.
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

            // Use ModelCapabilityService if available
            if (_capabilityService != null)
            {
                try
                {
                    var defaultModel = _capabilityService.GetDefaultModelAsync("openai", "tts").GetAwaiter().GetResult();
                    if (!string.IsNullOrWhiteSpace(defaultModel))
                        return defaultModel;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to get default TTS model via ModelCapabilityService");
                }
            }

            // Fallback to hardcoded default for backward compatibility
            return "tts-1";
        }

        /// <summary>
        /// Gets the default realtime model from configuration or falls back to gpt-4o-realtime-preview.
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

            // Use ModelCapabilityService if available
            if (_capabilityService != null)
            {
                try
                {
                    var defaultModel = _capabilityService.GetDefaultModelAsync("openai", "realtime").GetAwaiter().GetResult();
                    if (!string.IsNullOrWhiteSpace(defaultModel))
                        return defaultModel;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to get default realtime model via ModelCapabilityService");
                }
            }

            // Fallback to hardcoded default for backward compatibility
            return "gpt-4o-realtime-preview";
        }

        #endregion
    }

    // Azure-specific model response structures
    namespace AzureOpenAIModels
    {
        public class ListDeploymentsResponse
        {
            [JsonPropertyName("data")]
            public List<DeploymentInfo> Data { get; set; } = new();
        }

        public class DeploymentInfo
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;

            [JsonPropertyName("model")]
            public string Model { get; set; } = string.Empty;

            [JsonPropertyName("deploymentId")]
            public string DeploymentId { get; set; } = string.Empty;

            [JsonPropertyName("status")]
            public string Status { get; set; } = string.Empty;

            [JsonPropertyName("provisioningState")]
            public string ProvisioningState { get; set; } = string.Empty;
        }
    }
}
