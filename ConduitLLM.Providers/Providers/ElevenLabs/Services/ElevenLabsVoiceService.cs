using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Providers.ElevenLabs;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.ElevenLabs.Services
{
    /// <summary>
    /// Service for handling ElevenLabs voice management operations.
    /// </summary>
    internal class ElevenLabsVoiceService
    {
        private const string DEFAULT_BASE_URL = "https://api.elevenlabs.io/v1";
        private readonly ILogger _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public ElevenLabsVoiceService(ILogger logger, JsonSerializerOptions jsonOptions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _jsonOptions = jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions));
        }

        /// <summary>
        /// Lists available voices from ElevenLabs.
        /// </summary>
        public async Task<List<VoiceInfo>> ListVoicesAsync(
            HttpClient httpClient,
            string? baseUrl,
            CancellationToken cancellationToken = default)
        {
            var effectiveBaseUrl = baseUrl ?? DEFAULT_BASE_URL;
            var response = await httpClient.GetAsync($"{effectiveBaseUrl}/voices", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new LLMCommunicationException($"ElevenLabs API error: {response.StatusCode} - {errorContent}");
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var voicesResponse = JsonSerializer.Deserialize<ElevenLabsVoicesResponse>(jsonContent, _jsonOptions);

            return voicesResponse?.Voices?.Select(v => new VoiceInfo
            {
                VoiceId = v.VoiceId,
                Name = v.Name,
                SupportedLanguages = new List<string> { v.Labels?.Language ?? "en" },
                Gender = v.Labels?.Gender?.ToLower() switch
                {
                    "male" => VoiceGender.Male,
                    "female" => VoiceGender.Female,
                    _ => VoiceGender.Neutral
                },
                SampleUrl = v.PreviewUrl,
                Metadata = new Dictionary<string, object> { { "provider", "ElevenLabs" } }
            }).ToList() ?? new List<VoiceInfo>();
        }
    }
}