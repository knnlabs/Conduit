using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs.Audio;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service implementation for managing audio provider configurations.
    /// </summary>
    public class AdminAudioProviderService : IAdminAudioProviderService
    {
        private readonly IAudioProviderConfigRepository _repository;
        private readonly IProviderRepository _credentialRepository;
        private readonly ILLMClientFactory _clientFactory;
        private readonly ILogger<AdminAudioProviderService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminAudioProviderService"/> class.
        /// </summary>
        public AdminAudioProviderService(
            IAudioProviderConfigRepository repository,
            IProviderRepository credentialRepository,
            ILLMClientFactory clientFactory,
            ILogger<AdminAudioProviderService> logger)
        {
            _repository = repository;
            _credentialRepository = credentialRepository;
            _clientFactory = clientFactory;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<List<AudioProviderConfigDto>> GetAllAsync()
        {
            var configs = await _repository.GetAllAsync();
            return configs.Select(MapToDto).ToList();
        }

        /// <inheritdoc/>
        public async Task<AudioProviderConfigDto?> GetByIdAsync(int id)
        {
            var config = await _repository.GetByIdAsync(id);
            return config != null ? MapToDto(config) : null;
        }

        /// <inheritdoc/>
        public async Task<List<AudioProviderConfigDto>> GetByProviderAsync(int providerId)
        {
            var config = await _repository.GetByProviderIdAsync(providerId);
            return config != null ? new List<AudioProviderConfigDto> { MapToDto(config) } : new List<AudioProviderConfigDto>();
        }

        /// <inheritdoc/>
        public async Task<List<AudioProviderConfigDto>> GetEnabledForOperationAsync(string operationType)
        {
            var configs = await _repository.GetEnabledForOperationAsync(operationType);
            return configs.Select(MapToDto).ToList();
        }

        /// <inheritdoc/>
        public async Task<AudioProviderConfigDto> CreateAsync(CreateAudioProviderConfigDto dto)
        {
            // Validate that the provider credential exists
            var credential = await _credentialRepository.GetByIdAsync(dto.ProviderId);
            if (credential == null)
            {
                throw new ArgumentException($"Provider credential with ID {dto.ProviderId} not found");
            }

            // Check if configuration already exists for this credential
            if (await _repository.ExistsForProviderAsync(dto.ProviderId))
            {
                throw new ArgumentException($"Audio configuration already exists for provider credential {dto.ProviderId}");
            }

            var config = new AudioProviderConfig
            {
                ProviderId = dto.ProviderId,
                TranscriptionEnabled = dto.TranscriptionEnabled,
                DefaultTranscriptionModel = dto.DefaultTranscriptionModel,
                TextToSpeechEnabled = dto.TextToSpeechEnabled,
                DefaultTTSModel = dto.DefaultTTSModel,
                DefaultTTSVoice = dto.DefaultTTSVoice,
                RealtimeEnabled = dto.RealtimeEnabled,
                DefaultRealtimeModel = dto.DefaultRealtimeModel,
                RealtimeEndpoint = dto.RealtimeEndpoint,
                CustomSettings = dto.CustomSettings,
                RoutingPriority = dto.RoutingPriority
            };

            var created = await _repository.CreateAsync(config);
            _logger.LogInformation("Created audio provider configuration {Id} for provider ID {ProviderId}",
                created.Id, dto.ProviderId);

            return MapToDto(created);
        }

        /// <inheritdoc/>
        public async Task<AudioProviderConfigDto?> UpdateAsync(int id, UpdateAudioProviderConfigDto dto)
        {
            var config = await _repository.GetByIdAsync(id);
            if (config == null)
            {
                return null;
            }

            // Update properties
            config.TranscriptionEnabled = dto.TranscriptionEnabled;
            config.DefaultTranscriptionModel = dto.DefaultTranscriptionModel;
            config.TextToSpeechEnabled = dto.TextToSpeechEnabled;
            config.DefaultTTSModel = dto.DefaultTTSModel;
            config.DefaultTTSVoice = dto.DefaultTTSVoice;
            config.RealtimeEnabled = dto.RealtimeEnabled;
            config.DefaultRealtimeModel = dto.DefaultRealtimeModel;
            config.RealtimeEndpoint = dto.RealtimeEndpoint;
            config.CustomSettings = dto.CustomSettings;
            config.RoutingPriority = dto.RoutingPriority;

            var updated = await _repository.UpdateAsync(config);
            _logger.LogInformation("Updated audio provider configuration {Id}",
                id);

            return MapToDto(updated);
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(int id)
        {
            var deleted = await _repository.DeleteAsync(id);
            if (deleted)
            {
                _logger.LogInformation("Deleted audio provider configuration {Id}",
                id);
            }
            return deleted;
        }

        /// <inheritdoc/>
        public async Task<AudioProviderTestResult> TestProviderAsync(int id, string operationType)
        {
            var config = await _repository.GetByIdAsync(id);
            if (config == null)
            {
                throw new KeyNotFoundException($"Audio provider configuration {id} not found");
            }

            var result = new AudioProviderTestResult
            {
                Capabilities = new Dictionary<string, bool>()
            };

            try
            {
                var stopwatch = Stopwatch.StartNew();

                // Use the actual provider ID from the config
                var providerId = config.ProviderId;
                
                // Create a client for the provider
                var client = _clientFactory.GetClientByProviderId(providerId);

                // Test based on operation type
                switch (operationType.ToLower())
                {
                    case "transcription":
                        if (client is IAudioTranscriptionClient transcriptionClient)
                        {
                            try
                            {
                                var supported = await transcriptionClient.SupportsTranscriptionAsync();
                                result.Capabilities["transcription"] = supported;
                                if (supported)
                                {
                                    var formats = await transcriptionClient.GetSupportedFormatsAsync();
                                    result.Success = true;
                                    result.Message = $"Provider supports transcription with {formats.Count} audio formats";
                                }
                                else
                                {
                                    result.Success = false;
                                    result.Message = "Provider reports transcription is not supported";
                                }
                            }
                            catch (Exception ex)
                            {
                                result.Success = false;
                                result.Message = $"Failed to test transcription: {ex.Message}";
                            }
                        }
                        else
                        {
                            result.Success = false;
                            result.Message = "Provider does not support transcription";
                        }
                        break;

                    case "tts":
                    case "texttospeech":
                        if (client is ITextToSpeechClient ttsClient)
                        {
                            try
                            {
                                var voices = await ttsClient.ListVoicesAsync();
                                result.Capabilities["tts"] = voices.Any();
                                result.Success = true;
                                result.Message = $"Provider supports {voices.Count} TTS voices";
                            }
                            catch
                            {
                                result.Success = false;
                                result.Message = "Failed to retrieve TTS voices";
                            }
                        }
                        else
                        {
                            result.Success = false;
                            result.Message = "Provider does not support text-to-speech";
                        }
                        break;

                    case "realtime":
                        if (client is IRealtimeAudioClient realtimeClient)
                        {
                            try
                            {
                                var capabilities = await realtimeClient.GetCapabilitiesAsync();
                                result.Capabilities["realtime"] = true;
                                result.Capabilities["interruptions"] = capabilities.SupportsInterruptions;
                                result.Capabilities["functions"] = capabilities.SupportsFunctionCalling;
                                result.Success = true;
                                result.Message = "Provider supports real-time audio";
                            }
                            catch
                            {
                                result.Success = false;
                                result.Message = "Failed to retrieve real-time capabilities";
                            }
                        }
                        else
                        {
                            result.Success = false;
                            result.Message = "Provider does not support real-time audio";
                        }
                        break;

                    default:
                        result.Success = false;
                        result.Message = $"Unknown operation type: {operationType}";
                        break;
                }

                stopwatch.Stop();
                result.ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error testing audio provider {Id} for operation {Operation}",
                id,
                operationType.Replace(Environment.NewLine, ""));
                result.Success = false;
                result.Message = $"Test failed: {ex.Message}";
            }

            return result;
        }

        private static AudioProviderConfigDto MapToDto(AudioProviderConfig config)
        {
            return new AudioProviderConfigDto
            {
                Id = config.Id,
                ProviderId = config.ProviderId,
                ProviderType = config.Provider?.ProviderType,
                TranscriptionEnabled = config.TranscriptionEnabled,
                DefaultTranscriptionModel = config.DefaultTranscriptionModel,
                TextToSpeechEnabled = config.TextToSpeechEnabled,
                DefaultTTSModel = config.DefaultTTSModel,
                DefaultTTSVoice = config.DefaultTTSVoice,
                RealtimeEnabled = config.RealtimeEnabled,
                DefaultRealtimeModel = config.DefaultRealtimeModel,
                RealtimeEndpoint = config.RealtimeEndpoint,
                CustomSettings = config.CustomSettings,
                RoutingPriority = config.RoutingPriority,
                CreatedAt = config.CreatedAt,
                UpdatedAt = config.UpdatedAt
            };
        }

        private static string? GetDefaultModelForOperation(AudioProviderConfig config, string operationType)
        {
            return operationType.ToLower() switch
            {
                "transcription" => config.DefaultTranscriptionModel,
                "tts" or "texttospeech" => config.DefaultTTSModel,
                "realtime" => config.DefaultRealtimeModel,
                _ => null
            };
        }
    }
}
