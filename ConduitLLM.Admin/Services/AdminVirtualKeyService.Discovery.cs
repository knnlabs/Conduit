using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using Microsoft.EntityFrameworkCore;

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

            // Get all enabled model mappings with their related data
            using var context = await _dbContextFactory.CreateDbContextAsync();
            
            var modelMappings = await context.ModelProviderMappings
                .Include(m => m.Provider)
                .Include(m => m.Model)
                    .ThenInclude(m => m.Series)
                .Include(m => m.Model)
                    .ThenInclude(m => m.Capabilities)
                .Where(m => m.IsEnabled && m.Provider != null && m.Provider.IsEnabled)
                .ToListAsync();

            var models = new List<DiscoveredModelDto>();

            foreach (var mapping in modelMappings)
            {
                // Skip if model or capabilities are missing
                if (mapping.Model?.Capabilities == null)
                {
                    _logger.LogWarning("Model mapping {ModelAlias} has no model or capabilities data", mapping.ModelAlias);
                    continue;
                }

                var caps = mapping.Model.Capabilities;

                // Apply capability filter if specified
                if (!string.IsNullOrEmpty(capability))
                {
                    var capabilityKey = capability.Replace("-", "_").ToLowerInvariant();
                    bool hasCapability = capabilityKey switch
                    {
                        "chat" => caps.SupportsChat,
                        "streaming" or "chat_stream" => caps.SupportsStreaming,
                        "vision" => caps.SupportsVision,
                        "audio_transcription" => caps.SupportsAudioTranscription,
                        "text_to_speech" => caps.SupportsTextToSpeech,
                        "realtime_audio" => caps.SupportsRealtimeAudio,
                        "video_generation" => caps.SupportsVideoGeneration,
                        "image_generation" => caps.SupportsImageGeneration,
                        "embeddings" => caps.SupportsEmbeddings,
                        "function_calling" => caps.SupportsFunctionCalling,
                        _ => false
                    };

                    if (!hasCapability)
                    {
                        continue;
                    }
                }

                // Build flat capabilities structure matching DiscoveryController
                var capabilities = new Dictionary<string, object>
                {
                    ["supports_chat"] = caps.SupportsChat,
                    ["supports_streaming"] = caps.SupportsStreaming,
                    ["supports_vision"] = caps.SupportsVision,
                    ["supports_function_calling"] = caps.SupportsFunctionCalling,
                    ["supports_audio_transcription"] = caps.SupportsAudioTranscription,
                    ["supports_text_to_speech"] = caps.SupportsTextToSpeech,
                    ["supports_realtime_audio"] = caps.SupportsRealtimeAudio,
                    ["supports_video_generation"] = caps.SupportsVideoGeneration,
                    ["supports_image_generation"] = caps.SupportsImageGeneration,
                    ["supports_embeddings"] = caps.SupportsEmbeddings
                };

                // Add metadata
                capabilities["description"] = mapping.Model.Description ?? "";
                capabilities["model_card_url"] = mapping.Model.ModelCardUrl ?? "";
                capabilities["max_tokens"] = caps.MaxTokens;
                capabilities["tokenizer_type"] = caps.TokenizerType.ToString().ToLowerInvariant();

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

    }
}