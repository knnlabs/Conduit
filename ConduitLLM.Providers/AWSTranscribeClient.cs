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

using Amazon;
using Amazon.Polly;
using Amazon.Polly.Model;
using Amazon.TranscribeService;
using Amazon.TranscribeService.Model;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Providers.InternalModels;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Audio;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Client for interacting with AWS Transcribe and Polly services.
    /// </summary>
    public class AWSTranscribeClient : BaseLLMClient, IAudioTranscriptionClient, ITextToSpeechClient
    {
        private readonly string _region;
        private readonly AmazonTranscribeServiceClient _transcribeClient;
        private readonly AmazonPollyClient _pollyClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="AWSTranscribeClient"/> class.
        /// </summary>
        /// <param name="credentials">The provider credentials.</param>
        /// <param name="providerModelId">The provider's model identifier.</param>
        /// <param name="logger">The logger to use.</param>
        /// <param name="httpClientFactory">Optional HTTP client factory.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        public AWSTranscribeClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string providerModelId,
            ILogger<AWSTranscribeClient> logger,
            IHttpClientFactory? httpClientFactory = null,
            ProviderDefaultModels? defaultModels = null)
            : base(
                provider,
                keyCredential,
                providerModelId,
                logger,
                httpClientFactory,
                "aws",
                defaultModels)
        {
            // Extract region from provider.BaseUrl or use default
            _region = string.IsNullOrWhiteSpace(provider.BaseUrl) ? "us-east-1" : provider.BaseUrl;
            
            // Initialize AWS clients
            // For now, we'll use environment variables for AWS credentials
            // In production, use IAM roles or proper credential management
            var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "dummy-secret-key";
            var awsCredentials = new Amazon.Runtime.BasicAWSCredentials(
                keyCredential.ApiKey!,
                secretKey);
            
            var regionEndpoint = RegionEndpoint.GetBySystemName(_region);
            
            _transcribeClient = new AmazonTranscribeServiceClient(awsCredentials, regionEndpoint);
            _pollyClient = new AmazonPollyClient(awsCredentials, regionEndpoint);
        }

        /// <inheritdoc />
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            // Not used for AWS SDK clients
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");
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
                // AWS Transcribe requires audio to be in S3, so we'll use synchronous transcription
                // For production, you'd upload to S3 and use async transcription
                
                // For now, we'll simulate with a simple approach
                // Note: Real implementation would require S3 upload or streaming transcription
                
                if (request.AudioData == null)
                {
                    throw new ValidationException("Audio data is required for transcription");
                }

                // Create a unique job name
                var jobName = $"conduit-transcribe-{Guid.NewGuid():N}";
                
                // In a real implementation, we would:
                // 1. Upload audio to S3
                // 2. Start transcription job
                // 3. Wait for completion
                // 4. Retrieve results
                
                // For this example, we'll return a simulated response
                Logger.LogWarning("AWS Transcribe implementation is simplified. Production use requires S3 integration.");
                
                // Simulate transcription
                await Task.Delay(100, cancellationToken);
                
                return new AudioTranscriptionResponse
                {
                    Text = "This is a simulated transcription. Real AWS Transcribe integration requires S3 bucket setup.",
                    Language = request.Language ?? "en-US",
                    Duration = CalculateDuration(request.AudioData),
                    Segments = new List<TranscriptionSegment>
                    {
                        new TranscriptionSegment
                        {
                            Text = "This is a simulated transcription.",
                            Start = 0.0,
                            End = 2.0,
                            Confidence = 0.95f
                        }
                    }
                };
            }, "AudioTranscription", cancellationToken);
        }

        /// <inheritdoc />
        public async Task<bool> SupportsTranscriptionAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
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
                "wav",
                "mp3",
                "mp4",
                "flac",
                "ogg",
                "amr",
                "webm"
            };
        }

        /// <inheritdoc />
        public async Task<List<string>> GetSupportedLanguagesAsync(
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return new List<string>
            {
                "en-US", "en-GB", "en-AU", "en-IN", "es-US", "es-ES", 
                "fr-FR", "fr-CA", "de-DE", "it-IT", "pt-BR", "pt-PT",
                "ja-JP", "ko-KR", "zh-CN", "ar-SA", "hi-IN", "ru-RU"
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
                var synthesizeRequest = new SynthesizeSpeechRequest
                {
                    Text = request.Input,
                    OutputFormat = MapOutputFormat(request.ResponseFormat),
                    VoiceId = MapVoiceId(request.Voice),
                    LanguageCode = request.Language ?? "en-US",
                    Engine = request.Model?.Contains("neural") == true ? Engine.Neural : Engine.Standard,
                    SampleRate = "22050"
                };

                var response = await _pollyClient.SynthesizeSpeechAsync(synthesizeRequest, cancellationToken);
                
                if (response.AudioStream == null)
                {
                    throw new LLMCommunicationException("Failed to synthesize speech from AWS Polly");
                }

                // Read audio stream
                using var memoryStream = new MemoryStream();
                await response.AudioStream.CopyToAsync(memoryStream, cancellationToken);
                var audioData = memoryStream.ToArray();

                return new TextToSpeechResponse
                {
                    AudioData = audioData,
                    Format = (request.ResponseFormat ?? AudioFormat.Mp3).ToString().ToLower(),
                    Duration = EstimateDuration(audioData, request.ResponseFormat ?? AudioFormat.Mp3),
                    ModelUsed = request.Model ?? "standard",
                    VoiceUsed = request.Voice ?? "Joanna"
                };
            }, "TextToSpeech", cancellationToken);
        }

        /// <inheritdoc />
        public async Task<bool> SupportsTextToSpeechAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return true;
        }

        /// <inheritdoc />
        public IAsyncEnumerable<AudioChunk> StreamSpeechAsync(
            TextToSpeechRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("AWS Polly streaming is not implemented in this client");
        }

        // GetSupportedFormatsAsync is implemented in IAudioTranscriptionClient section

        /// <inheritdoc />
        public async Task<List<VoiceInfo>> ListVoicesAsync(
            string? language = null,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteApiRequestAsync(async () =>
            {
                var describeVoicesRequest = new DescribeVoicesRequest();
                
                if (!string.IsNullOrWhiteSpace(language))
                {
                    describeVoicesRequest.LanguageCode = language;
                }

                var response = await _pollyClient.DescribeVoicesAsync(describeVoicesRequest, cancellationToken);
                
                return response.Voices.Select(v => new VoiceInfo
                {
                    VoiceId = v.Id.Value,
                    Name = v.Name,
                    SupportedLanguages = new List<string> { v.LanguageCode.Value },
                    Gender = MapGender(v.Gender.Value),
                    SupportedStyles = v.SupportedEngines?.Select(e => e).ToList() ?? new List<string>()
                }).ToList();
            }, "GetAvailableVoices", cancellationToken);
        }

        /// <inheritdoc />
        public override Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("AWS Transcribe client does not support chat completions");
        }

        /// <inheritdoc />
        public override IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("AWS Transcribe client does not support streaming chat completions");
        }

        /// <inheritdoc />
        public override Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("AWS Transcribe client does not support embeddings");
        }

        /// <inheritdoc />
        public override Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("AWS Transcribe client does not support image generation");
        }

        /// <inheritdoc />
        public override async Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return new List<ExtendedModelInfo>
            {
                ExtendedModelInfo.Create("standard", ProviderName, "standard"),
                ExtendedModelInfo.Create("neural", ProviderName, "neural")
            };
        }

        #region Helper Methods

        private VoiceGender? MapGender(string gender)
        {
            return gender?.ToLowerInvariant() switch
            {
                "male" => VoiceGender.Male,
                "female" => VoiceGender.Female,
                "neutral" => VoiceGender.Neutral,
                _ => null
            };
        }

        private OutputFormat MapOutputFormat(AudioFormat? format)
        {
            return format switch
            {
                AudioFormat.Mp3 => OutputFormat.Mp3,
                AudioFormat.Ogg => OutputFormat.Mp3, // AWS Polly doesn't have Ogg, use Mp3
                AudioFormat.Pcm => OutputFormat.Pcm,
                _ => OutputFormat.Mp3
            };
        }

        private VoiceId MapVoiceId(string? voice)
        {
            if (string.IsNullOrWhiteSpace(voice))
                return VoiceId.Joanna;

            // Try to find matching voice
            return VoiceId.FindValue(voice) ?? VoiceId.Joanna;
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
                AudioFormat.Pcm => 256000, // 256 kbps  
                AudioFormat.Ogg => 96000,  // 96 kbps
                _ => 128000
            };

            return (audioData.Length * 8.0) / bitrate;
        }

        #endregion

        /// <summary>
        /// Disposes the AWS clients.
        /// </summary>
        public void DisposeClients()
        {
            _transcribeClient?.Dispose();
            _pollyClient?.Dispose();
        }
    }
}