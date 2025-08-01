using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Core.Utilities;
using ConduitLLM.Providers.InternalModels.GoogleCloudModels;
using ConduitLLM.Providers.InternalModels;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Client for interacting with Google Cloud Speech-to-Text and Text-to-Speech APIs.
    /// </summary>
    public class GoogleCloudAudioClient : BaseLLMClient, IAudioTranscriptionClient, ITextToSpeechClient
    {
        private const string SpeechToTextBaseUrl = "https://speech.googleapis.com/v1/";
        private const string TextToSpeechBaseUrl = "https://texttospeech.googleapis.com/v1/";

        /// <summary>
        /// Initializes a new instance of the <see cref="GoogleCloudAudioClient"/> class.
        /// </summary>
        /// <param name="credentials">The provider credentials.</param>
        /// <param name="providerModelId">The provider's model identifier.</param>
        /// <param name="logger">The logger to use.</param>
        /// <param name="httpClientFactory">Optional HTTP client factory.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        public GoogleCloudAudioClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string providerModelId,
            ILogger<GoogleCloudAudioClient> logger,
            IHttpClientFactory? httpClientFactory = null,
            ProviderDefaultModels? defaultModels = null)
            : base(
                provider,
                keyCredential,
                providerModelId,
                logger,
                httpClientFactory,
                "googlecloud",
                defaultModels)
        {
        }

        /// <inheritdoc />
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");
            
            // Google Cloud uses API key as query parameter, not in headers
            // We'll add it when making requests
        }

        /// <inheritdoc />
        public async Task<AudioTranscriptionResponse> TranscribeAudioAsync(
            AudioTranscriptionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "AudioTranscription");

            return await ExecuteApiRequestAsync(async () =>
            {
                // Convert audio to base64 if provided as bytes
                string audioContent;
                if (request.AudioData != null)
                {
                    audioContent = Convert.ToBase64String(request.AudioData);
                }
                else
                {
                    throw new ValidationException("Audio data is required for transcription");
                }

                // Create Google Cloud Speech-to-Text request
                var gcRequest = new GoogleCloudSpeechRequest
                {
                    Config = new GoogleCloudSpeechConfig
                    {
                        Encoding = MapAudioFormat(request.AudioFormat),
                        SampleRateHertz = 16000, // Default sample rate
                        LanguageCode = request.Language ?? "en-US",
                        EnableAutomaticPunctuation = true,
                        EnableWordTimeOffsets = request.TimestampGranularity == TimestampGranularity.Word,
                        Model = request.Model ?? "latest_long",
                        UseEnhanced = true
                    },
                    Audio = new GoogleCloudAudioContent
                    {
                        Content = audioContent
                    }
                };

                using var client = CreateHttpClient(apiKey);
                var baseUrl = Provider.BaseUrl ?? SpeechToTextBaseUrl;
                client.BaseAddress = new Uri(baseUrl);

                var effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : PrimaryKeyCredential.ApiKey;
                var requestUrl = $"speech:recognize?key={effectiveApiKey}";

                var response = await HttpClientHelper.SendJsonRequestAsync<GoogleCloudSpeechRequest, GoogleCloudSpeechResponse>(
                    client,
                    HttpMethod.Post,
                    requestUrl,
                    gcRequest,
                    null,
                    DefaultJsonOptions,
                    Logger,
                    cancellationToken);

                if (response == null || response.Results == null)
                {
                    throw new LLMCommunicationException("Failed to get transcription results from Google Cloud Speech-to-Text");
                }

                // Convert to standard response format
                var transcription = string.Join(" ", response.Results
                    .Where(r => r.Alternatives != null && r.Alternatives.Any())
                    .Select(r => r.Alternatives!.First().Transcript));

                var audioResponse = new AudioTranscriptionResponse
                {
                    Text = transcription,
                    Language = request.Language ?? "en-US",
                    Duration = CalculateDuration(request.AudioData),
                    Segments = ConvertToSegments(response.Results),
                    Words = request.TimestampGranularity == TimestampGranularity.Word 
                        ? ConvertToWords(response.Results) 
                        : null
                };

                return audioResponse;
            }, "AudioTranscription", cancellationToken);
        }

        /// <inheritdoc />
        public async Task<bool> SupportsTranscriptionAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Google Cloud Speech-to-Text is always available with valid API key
            await Task.CompletedTask;
            return true;
        }

        /// <inheritdoc />
        public async Task<List<string>> GetSupportedFormatsAsync(
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return new List<string>
            {
                "flac",
                "wav",
                "mp3",
                "ogg",
                "webm",
                "m4a",
                "amr",
                "amr-wb"
            };
        }

        /// <inheritdoc />
        public async Task<List<string>> GetSupportedLanguagesAsync(
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return new List<string>
            {
                "en-US", "en-GB", "es-ES", "fr-FR", "de-DE", "it-IT", 
                "pt-BR", "pt-PT", "ru-RU", "ja-JP", "ko-KR", "zh-CN",
                "ar-SA", "hi-IN", "nl-NL", "pl-PL", "tr-TR", "sv-SE"
            };
        }

        /// <inheritdoc />
        public async Task<TextToSpeechResponse> CreateSpeechAsync(
            TextToSpeechRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "TextToSpeech");

            return await ExecuteApiRequestAsync(async () =>
            {
                // Create Google Cloud Text-to-Speech request
                var gcRequest = new GoogleCloudTTSRequest
                {
                    Input = new GoogleCloudTTSInput
                    {
                        Text = request.Input
                    },
                    Voice = new GoogleCloudTTSVoice
                    {
                        LanguageCode = request.Language ?? "en-US",
                        Name = request.Voice ?? "en-US-Wavenet-D",
                        SsmlGender = MapGender(request.Voice)
                    },
                    AudioConfig = new GoogleCloudTTSAudioConfig
                    {
                        AudioEncoding = MapOutputFormat(request.ResponseFormat),
                        SpeakingRate = request.Speed ?? 1.0,
                        Pitch = request.Pitch ?? 0.0,
                        VolumeGainDb = 0.0
                    }
                };

                using var client = CreateHttpClient(apiKey);
                var baseUrl = Provider.BaseUrl ?? TextToSpeechBaseUrl;
                client.BaseAddress = new Uri(baseUrl);

                var effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : PrimaryKeyCredential.ApiKey;
                var requestUrl = $"text:synthesize?key={effectiveApiKey}";

                var response = await HttpClientHelper.SendJsonRequestAsync<GoogleCloudTTSRequest, GoogleCloudTTSResponse>(
                    client,
                    HttpMethod.Post,
                    requestUrl,
                    gcRequest,
                    null,
                    DefaultJsonOptions,
                    Logger,
                    cancellationToken);

                if (response == null || string.IsNullOrEmpty(response.AudioContent))
                {
                    throw new LLMCommunicationException("Failed to synthesize speech from Google Cloud Text-to-Speech");
                }

                // Convert base64 audio content to bytes
                var audioData = Convert.FromBase64String(response.AudioContent);

                return new TextToSpeechResponse
                {
                    AudioData = audioData,
                    Format = (request.ResponseFormat ?? AudioFormat.Mp3).ToString().ToLower(),
                    Duration = EstimateDuration(audioData, request.ResponseFormat ?? AudioFormat.Mp3),
                    ModelUsed = request.Model ?? "wavenet",
                    VoiceUsed = request.Voice ?? "en-US-Wavenet-D"
                };
            }, "TextToSpeech", cancellationToken);
        }

        /// <inheritdoc />
        public async Task<bool> SupportsTextToSpeechAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Google Cloud Text-to-Speech is always available with valid API key
            await Task.CompletedTask;
            return true;
        }

        /// <inheritdoc />
        public IAsyncEnumerable<AudioChunk> StreamSpeechAsync(
            TextToSpeechRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Google Cloud Text-to-Speech does not support streaming synthesis");
        }

        // GetSupportedFormatsAsync is implemented in IAudioTranscriptionClient section

        /// <inheritdoc />
        public async Task<List<VoiceInfo>> ListVoicesAsync(
            string? language = null,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteApiRequestAsync(async () =>
            {
                using var client = CreateHttpClient();
                var baseUrl = Provider.BaseUrl ?? TextToSpeechBaseUrl;
                client.BaseAddress = new Uri(baseUrl);

                var effectiveApiKey = PrimaryKeyCredential.ApiKey;
                var requestUrl = $"voices?key={effectiveApiKey}";
                
                if (!string.IsNullOrWhiteSpace(language))
                {
                    requestUrl += $"&languageCode={language}";
                }

                var response = await client.GetAsync(requestUrl, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new LLMCommunicationException(
                        $"Failed to get voices from Google Cloud: {response.StatusCode} - {errorContent}");
                }

                var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
                var voicesResponse = JsonSerializer.Deserialize<GoogleCloudVoicesResponse>(
                    jsonResponse, DefaultJsonOptions);

                if (voicesResponse?.Voices == null)
                {
                    return new List<VoiceInfo>();
                }

                return voicesResponse.Voices.Select(v => new VoiceInfo
                {
                    VoiceId = v.Name,
                    Name = v.Name,
                    SupportedLanguages = v.LanguageCodes ?? new List<string>(),
                    Gender = MapGenderFromString(v.SsmlGender),
                    SupportedStyles = new List<string>()
                }).ToList();
            }, "GetAvailableVoices", cancellationToken);
        }

        /// <inheritdoc />
        public override Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Google Cloud Audio client does not support chat completions");
        }

        /// <inheritdoc />
        public override IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Google Cloud Audio client does not support streaming chat completions");
        }

        /// <inheritdoc />
        public override Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Google Cloud Audio client does not support embeddings");
        }

        /// <inheritdoc />
        public override Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Google Cloud Audio client does not support image generation");
        }

        /// <summary>
        /// Verifies Google Cloud authentication by listing available voices.
        /// This is a free API call that validates credentials.
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
                client.BaseAddress = new Uri(TextToSpeechBaseUrl);
                
                // Use the voices:list endpoint which is free and validates the API key
                var request = new HttpRequestMessage(HttpMethod.Get, $"voices?key={effectiveApiKey}");
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                
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
                    return Core.Interfaces.AuthenticationResult.Failure(
                        "Access denied. Ensure Cloud Text-to-Speech API is enabled and API key has permissions");
                }
                
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Google Cloud authentication failed: {response.StatusCode}",
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
                Logger.LogError(ex, "Unexpected error during Google Cloud authentication verification");
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Authentication verification failed: {ex.Message}",
                    ex.ToString());
            }
        }

        /// <inheritdoc />
        public override async Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return new List<ExtendedModelInfo>
            {
                ExtendedModelInfo.Create("latest_long", ProviderName, "latest_long"),
                ExtendedModelInfo.Create("latest_short", ProviderName, "latest_short"),
                ExtendedModelInfo.Create("command_and_search", ProviderName, "command_and_search"),
                ExtendedModelInfo.Create("phone_call", ProviderName, "phone_call"),
                ExtendedModelInfo.Create("video", ProviderName, "video"),
                ExtendedModelInfo.Create("medical_conversation", ProviderName, "medical_conversation"),
                ExtendedModelInfo.Create("medical_dictation", ProviderName, "medical_dictation")
            };
        }

        #region Helper Methods

        private string MapAudioFormat(AudioFormat? format)
        {
            return format switch
            {
                AudioFormat.Flac => "FLAC",
                AudioFormat.Wav => "LINEAR16",
                AudioFormat.Mp3 => "MP3",
                AudioFormat.Ogg => "OGG_OPUS",
                // AudioFormat.Webm => "WEBM_OPUS", // Webm not in enum
                _ => "LINEAR16"
            };
        }

        private string MapOutputFormat(AudioFormat? format)
        {
            return format switch
            {
                AudioFormat.Mp3 => "MP3",
                AudioFormat.Wav => "LINEAR16",
                AudioFormat.Ogg => "OGG_OPUS",
                _ => "MP3"
            };
        }

        private string MapGender(string? voice)
        {
            if (string.IsNullOrWhiteSpace(voice))
                return "NEUTRAL";

            var lowerVoice = voice.ToLower();
            if (lowerVoice.Contains("female") || lowerVoice.Contains("woman"))
                return "FEMALE";
            if (lowerVoice.Contains("male") || lowerVoice.Contains("man"))
                return "MALE";
            
            return "NEUTRAL";
        }

        private double CalculateDuration(byte[]? audioData)
        {
            // This is a rough estimate - actual duration would depend on format and bitrate
            if (audioData == null || audioData.Length == 0)
                return 0.0;

            // Assume ~16kbps for speech audio
            return audioData.Length / 2000.0; // Very rough estimate
        }

        private double EstimateDuration(byte[] audioData, AudioFormat format)
        {
            // Rough estimation based on typical bitrates
            var bitrate = format switch
            {
                AudioFormat.Mp3 => 128000, // 128 kbps
                AudioFormat.Wav => 256000, // 256 kbps  
                AudioFormat.Ogg => 96000,  // 96 kbps
                _ => 128000
            };

            return (audioData.Length * 8.0) / bitrate;
        }

        private List<TranscriptionSegment>? ConvertToSegments(List<GoogleCloudSpeechResult>? results)
        {
            if (results == null || !results.Any())
                return null;

            var segments = new List<TranscriptionSegment>();
            
            foreach (var result in results.Where(r => r.Alternatives != null && r.Alternatives.Any()))
            {
                var alternative = result.Alternatives!.First();
                segments.Add(new TranscriptionSegment
                {
                    Text = alternative.Transcript,
                    Start = 0.0, // Google doesn't provide segment timestamps
                    End = 0.0,
                    Confidence = alternative.Confidence
                });
            }

            return segments.Any() ? segments : null;
        }

        private List<TranscriptionWord>? ConvertToWords(List<GoogleCloudSpeechResult>? results)
        {
            if (results == null || !results.Any())
                return null;

            var words = new List<TranscriptionWord>();
            
            foreach (var result in results.Where(r => r.Alternatives != null && r.Alternatives.Any()))
            {
                var alternative = result.Alternatives!.First();
                if (alternative.Words != null)
                {
                    words.AddRange(alternative.Words.Select(w => new TranscriptionWord
                    {
                        Word = w.Word,
                        Start = w.StartTime ?? 0.0,
                        End = w.EndTime ?? 0.0,
                        Confidence = w.Confidence
                    }));
                }
            }

            return words.Any() ? words : null;
        }

        #endregion
        private VoiceGender? MapGenderFromString(string? gender)
        {
            return gender?.ToUpperInvariant() switch
            {
                "MALE" => VoiceGender.Male,
                "FEMALE" => VoiceGender.Female,
                "NEUTRAL" => VoiceGender.Neutral,
                _ => null
            };
        }
    }
}