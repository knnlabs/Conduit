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

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Providers.Helpers;
using ConduitLLM.Providers.Providers.OpenAI.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Providers.OpenAI
{
    /// <summary>
    /// OpenAIClient partial class containing audio transcription and text-to-speech functionality.
    /// </summary>
    public partial class OpenAIClient
    {
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
                : UrlBuilder.Combine(BaseUrl, Constants.Endpoints.AudioTranscriptions);

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
                var jsonResponse = JsonSerializer.Deserialize<ConduitLLM.Providers.Providers.OpenAI.Models.TranscriptionResponse>(responseText, DefaultJsonOptions);

                return new AudioTranscriptionResponse
                {
                    Text = jsonResponse?.Text ?? string.Empty,
                    Language = jsonResponse?.Language,
                    Duration = jsonResponse?.Duration,
                    Model = request.Model ?? "whisper-1",
                    Segments = jsonResponse?.Segments?.Select(s => new ConduitLLM.Core.Models.Audio.TranscriptionSegment
                    {
                        Id = s.Id,
                        Start = s.Start,
                        End = s.End,
                        Text = s.Text
                    }).ToList(),
                    Words = jsonResponse?.Words?.Select(w => new ConduitLLM.Core.Models.Audio.TranscriptionWord
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
            ConduitLLM.Core.Models.Audio.TextToSpeechRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateSpeech");

            using var client = CreateHttpClient(apiKey);

            var endpoint = _isAzure
                ? GetAzureAudioEndpoint("speech")
                : UrlBuilder.Combine(BaseUrl, Constants.Endpoints.AudioSpeech);

            var openAIRequest = new ConduitLLM.Providers.Providers.OpenAI.Models.TextToSpeechRequest
            {
                Model = request.Model ?? GetDefaultTextToSpeechModel(),
                Input = request.Input,
                Voice = request.Voice,
                ResponseFormat = MapAudioFormat(request.ResponseFormat),
                Speed = request.Speed
            };

            var json = JsonSerializer.Serialize(openAIRequest, DefaultJsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

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
            ConduitLLM.Core.Models.Audio.TextToSpeechRequest request,
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
            var url = UrlBuilder.Combine(BaseUrl, "openai", "deployments", ProviderModelId, "audio", operation);
            return UrlBuilder.AppendQueryString(url, ("api-version", Constants.AzureApiVersion));
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
    }
}