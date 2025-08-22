using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models.Audio;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.ElevenLabs
{
    /// <summary>
    /// Service for handling ElevenLabs text-to-speech operations.
    /// </summary>
    internal class ElevenLabsTextToSpeechService
    {
        private const string DEFAULT_BASE_URL = "https://api.elevenlabs.io/v1";
        private readonly ILogger _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public ElevenLabsTextToSpeechService(ILogger logger, JsonSerializerOptions jsonOptions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _jsonOptions = jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions));
        }

        /// <summary>
        /// Creates speech audio from text using ElevenLabs.
        /// </summary>
        public async Task<TextToSpeechResponse> CreateSpeechAsync(
            HttpClient httpClient,
            string? baseUrl,
            TextToSpeechRequest request,
            string model,
            CancellationToken cancellationToken = default)
        {
            // ElevenLabs uses voice IDs instead of voice names
            var voiceId = request.Voice ?? "21m00Tcm4TlvDq8ikWAM"; // Default voice ID

            var effectiveBaseUrl = baseUrl ?? DEFAULT_BASE_URL;
            var requestUrl = $"{effectiveBaseUrl}/text-to-speech/{voiceId}";

            var requestBody = new Dictionary<string, object>
            {
                ["text"] = request.Input,
                ["model_id"] = model,
                ["voice_settings"] = new Dictionary<string, object>
                {
                    ["stability"] = request.VoiceSettings?.Stability ?? 0.5,
                    ["similarity_boost"] = request.VoiceSettings?.SimilarityBoost ?? 0.5,
                    ["style"] = request.VoiceSettings?.Style ?? "default"
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestBody, _jsonOptions);
            using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(requestUrl, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new LLMCommunicationException($"ElevenLabs API error: {response.StatusCode} - {errorContent}");
            }

            var audioData = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            return new TextToSpeechResponse
            {
                AudioData = audioData,
                Format = request.ResponseFormat?.ToString().ToLower() ?? "mp3",
                SampleRate = 22050, // ElevenLabs default
                Duration = null // Would need to calculate from audio data
            };
        }

        /// <summary>
        /// Streams speech audio from text using ElevenLabs.
        /// </summary>
        public async IAsyncEnumerable<AudioChunk> StreamSpeechAsync(
            HttpClient httpClient,
            string? baseUrl,
            TextToSpeechRequest request,
            string model,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var voiceId = request.Voice ?? "21m00Tcm4TlvDq8ikWAM";

            var effectiveBaseUrl = baseUrl ?? DEFAULT_BASE_URL;
            var requestUrl = $"{effectiveBaseUrl}/text-to-speech/{voiceId}/stream";

            var requestBody = new Dictionary<string, object>
            {
                ["text"] = request.Input,
                ["model_id"] = model,
                ["voice_settings"] = new Dictionary<string, object>
                {
                    ["stability"] = request.VoiceSettings?.Stability ?? 0.5,
                    ["similarity_boost"] = request.VoiceSettings?.SimilarityBoost ?? 0.5
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestBody, _jsonOptions);
            using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(requestUrl, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new LLMCommunicationException($"ElevenLabs API error: {response.StatusCode} - {errorContent}");
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[4096];
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                var chunk = new byte[bytesRead];
                Array.Copy(buffer, 0, chunk, 0, bytesRead);

                yield return new AudioChunk
                {
                    Data = chunk,
                    IsFinal = false
                };
            }

            // Final chunk
            yield return new AudioChunk
            {
                Data = Array.Empty<byte>(),
                IsFinal = true
            };
        }

        /// <summary>
        /// Gets the audio formats supported by ElevenLabs.
        /// </summary>
        public Task<List<string>> GetSupportedFormatsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<string>
            {
                "mp3",
                "wav",
                "pcm",
                "ogg",
                "flac"
            });
        }

        /// <summary>
        /// Checks if the client supports text-to-speech synthesis.
        /// </summary>
        public Task<bool> SupportsTextToSpeechAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }
}