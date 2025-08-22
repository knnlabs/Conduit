using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service for managing virtual keys through the Admin API - Discovery functionality
    /// </summary>
    public partial class AdminVirtualKeyService
    {
        /// <inheritdoc />
        public async Task<VirtualKeyDiscoveryPreviewDto?> PreviewDiscoveryAsync(int id, string? capability = null)
        {
            _logger.LogInformation("Previewing discovery for virtual key {KeyId} with capability filter: {Capability}", 
                id, capability ?? "none");

            // Get the virtual key
            var virtualKey = await _virtualKeyRepository.GetByIdAsync(id);
            if (virtualKey == null)
            {
                _logger.LogWarning("Virtual key with ID {KeyId} not found", id);
                return null;
            }

            // Get all model mappings
            var allMappings = await _modelProviderMappingRepository.GetAllAsync();
            var enabledMappings = allMappings.Where(m => 
                m.IsEnabled && 
                m.Provider != null && 
                m.Provider.IsEnabled).ToList();

            var models = new List<DiscoveredModelDto>();

            // Check each model mapping against the virtual key's allowed models
            foreach (var mapping in enabledMappings)
            {
                // Check if model is allowed for this virtual key
                if (!IsModelAllowed(virtualKey, mapping.ModelAlias))
                {
                    continue;
                }

                // Build capabilities dictionary for this model
                var capabilities = await BuildCapabilitiesAsync(mapping.ModelAlias);

                // Filter by capability if specified
                if (!string.IsNullOrEmpty(capability))
                {
                    if (!capabilities.ContainsKey(capability))
                    {
                        continue;
                    }

                    if (capabilities[capability] is Dictionary<string, object> capDict)
                    {
                        if (!capDict.ContainsKey("supported") || capDict["supported"] is not bool supported || !supported)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        continue;
                    }
                }

                var model = new DiscoveredModelDto
                {
                    Id = mapping.ModelAlias,
                    DisplayName = mapping.ModelAlias,
                    Capabilities = capabilities
                };

                models.Add(model);
            }

            return new VirtualKeyDiscoveryPreviewDto
            {
                Data = models,
                Count = models.Count()
            };
        }

        /// <summary>
        /// Builds the capabilities dictionary for a model
        /// </summary>
        private async Task<Dictionary<string, object>> BuildCapabilitiesAsync(string modelAlias)
        {
            var capabilities = new Dictionary<string, object>();

            // Basic capabilities - always included for all models
            capabilities["chat"] = new Dictionary<string, object> { ["supported"] = true };
            capabilities["chat_stream"] = new Dictionary<string, object> { ["supported"] = true };

            // Check vision support
            if (await _modelCapabilityService.SupportsVisionAsync(modelAlias))
            {
                capabilities["vision"] = new Dictionary<string, object> { ["supported"] = true };
            }

            // Check audio transcription support
            if (await _modelCapabilityService.SupportsAudioTranscriptionAsync(modelAlias))
            {
                var audioCapabilities = new Dictionary<string, object> { ["supported"] = true };
                
                var supportedLanguages = await _modelCapabilityService.GetSupportedLanguagesAsync(modelAlias);
                if (supportedLanguages.Count() > 0)
                {
                    audioCapabilities["supported_languages"] = supportedLanguages;
                }

                var supportedFormats = await _modelCapabilityService.GetSupportedFormatsAsync(modelAlias);
                if (supportedFormats.Count() > 0)
                {
                    audioCapabilities["supported_formats"] = supportedFormats;
                }

                capabilities["audio_transcription"] = audioCapabilities;
            }

            // Check text-to-speech support
            if (await _modelCapabilityService.SupportsTextToSpeechAsync(modelAlias))
            {
                var ttsCapabilities = new Dictionary<string, object> { ["supported"] = true };
                
                var supportedVoices = await _modelCapabilityService.GetSupportedVoicesAsync(modelAlias);
                if (supportedVoices.Count() > 0)
                {
                    ttsCapabilities["supported_voices"] = supportedVoices;
                }

                var supportedLanguages = await _modelCapabilityService.GetSupportedLanguagesAsync(modelAlias);
                if (supportedLanguages.Count() > 0)
                {
                    ttsCapabilities["supported_languages"] = supportedLanguages;
                }

                capabilities["text_to_speech"] = ttsCapabilities;
            }

            // Check realtime audio support
            if (await _modelCapabilityService.SupportsRealtimeAudioAsync(modelAlias))
            {
                capabilities["realtime_audio"] = new Dictionary<string, object> { ["supported"] = true };
            }

            // Check video generation support
            if (await _modelCapabilityService.SupportsVideoGenerationAsync(modelAlias))
            {
                capabilities["video_generation"] = new Dictionary<string, object> 
                { 
                    ["supported"] = true,
                    ["max_duration_seconds"] = 6,
                    ["supported_resolutions"] = new List<string> { "720x480", "1280x720", "1920x1080" },
                    ["supported_fps"] = new List<int> { 24, 30 },
                    ["supports_custom_styles"] = true
                };
            }

            // TODO: Add image generation support when method is available
            // For now, check if model contains "dall-e" or similar patterns
            if (modelAlias.Contains("dall-e", StringComparison.OrdinalIgnoreCase) ||
                modelAlias.Contains("stable-diffusion", StringComparison.OrdinalIgnoreCase) ||
                modelAlias.Contains("midjourney", StringComparison.OrdinalIgnoreCase))
            {
                capabilities["image_generation"] = new Dictionary<string, object>
                {
                    ["supported"] = true,
                    ["supported_sizes"] = new List<string> { "256x256", "512x512", "1024x1024", "1024x1792", "1792x1024" }
                };
            }

            return capabilities;
        }

        /// <summary>
        /// Checks if a model is allowed for a virtual key based on AllowedModels restrictions
        /// </summary>
        private bool IsModelAllowed(VirtualKey virtualKey, string modelAlias)
        {
            // If no AllowedModels specified, all models are allowed
            if (string.IsNullOrWhiteSpace(virtualKey.AllowedModels))
            {
                return true;
            }

            var allowedModels = virtualKey.AllowedModels
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(m => m.Trim())
                .ToList();

            foreach (var allowedModel in allowedModels)
            {
                // Check for exact match
                if (allowedModel.Equals(modelAlias, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Check for wildcard/prefix match (e.g., "gpt-4*")
                if (allowedModel.EndsWith("*"))
                {
                    var prefix = allowedModel.Substring(0, allowedModel.Length - 1);
                    if (modelAlias.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}