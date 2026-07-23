using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Models;
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

            using var context = await _dbContextFactory.CreateDbContextAsync();

            var projections = await context.ModelProviderMappings
                .AsNoTracking()
                .Where(m => m.IsEnabled && m.Provider != null && m.Provider.IsEnabled)
                .Select(m => new
                {
                    m.ModelAlias,
                    Association = m.ModelProviderTypeAssociation,
                    Model = m.ModelProviderTypeAssociation != null
                        ? m.ModelProviderTypeAssociation.Model
                        : null
                })
                .ToListAsync();

            var models = new List<DiscoveredModelDto>();

            foreach (var p in projections)
            {
                if (p.Association == null || p.Model == null)
                {
                    _logger.LogWarning("Model mapping {ModelAlias} has no model data", p.ModelAlias);
                    continue;
                }

                var model = p.Model;
                var caps = ModelCapabilityResolver.Resolve(model, p.Association);

                if (!string.IsNullOrEmpty(capability))
                {
                    var capabilityKey = capability.Replace("-", "_").ToLowerInvariant();
                    bool hasCapability = capabilityKey switch
                    {
                        "chat" => caps.SupportsChat,
                        "streaming" or "chat_stream" => caps.SupportsStreaming,
                        "vision" or "image_input" => caps.SupportsImageInput,
                        "video_input" or "video_understanding" => caps.SupportsVideoInput,
                        "audio_input" => caps.SupportsAudioInput,
                        "file_input" => caps.SupportsFileInput,
                        "video_generation" => caps.SupportsVideoGeneration,
                        "image_generation" => caps.SupportsImageGeneration,
                        "embeddings" => caps.SupportsEmbeddings,
                        "function_calling" => caps.SupportsFunctionCalling,
                        "speech_to_text" => caps.SupportsSpeechToText,
                        "text_to_speech" => caps.SupportsTextToSpeech,
                        "rerank" => caps.SupportsRerank,
                        _ => false
                    };

                    if (!hasCapability)
                    {
                        continue;
                    }
                }

                var capabilities = new Dictionary<string, object?>
                {
                    ["input_modalities"] = caps.InputModalities,
                    ["output_modalities"] = caps.OutputModalities,
                    ["capability_source"] = caps.Source.ToString(),
                    ["capabilities_last_verified_at"] = caps.LastVerifiedAt?.ToString("O") ?? "",
                    ["supports_chat"] = caps.SupportsChat,
                    ["supports_streaming"] = caps.SupportsStreaming,
                    ["supports_vision"] = caps.SupportsImageInput,
                    ["supports_image_input"] = caps.SupportsImageInput,
                    ["supports_video_input"] = caps.SupportsVideoInput,
                    ["supports_video_understanding"] = caps.SupportsVideoUnderstanding,
                    ["supports_audio_input"] = caps.SupportsAudioInput,
                    ["supports_file_input"] = caps.SupportsFileInput,
                    ["supports_function_calling"] = caps.SupportsFunctionCalling,
                    ["supports_video_generation"] = caps.SupportsVideoGeneration,
                    ["supports_image_generation"] = caps.SupportsImageGeneration,
                    ["supports_embeddings"] = caps.SupportsEmbeddings,
                    ["supports_speech_to_text"] = caps.SupportsSpeechToText,
                    ["supports_text_to_speech"] = caps.SupportsTextToSpeech,
                    ["supports_rerank"] = caps.SupportsRerank,
                    ["description"] = model.Description ?? "",
                    ["model_card_url"] = model.ModelCardUrl ?? "",
                    ["input_tokens"] = p.Association.MaxInputTokens ?? model.MaxInputTokens ?? 0,
                    ["output_tokens"] = p.Association.MaxOutputTokens ?? model.MaxOutputTokens ?? 0,
                    ["tokenizer_type"] = model.TokenizerType.ToString().ToLowerInvariant()
                };

                models.Add(new DiscoveredModelDto
                {
                    Id = p.ModelAlias,
                    DisplayName = p.ModelAlias,
                    Capabilities = capabilities
                });
            }

            return new VirtualKeyDiscoveryPreviewDto
            {
                Data = models,
                Count = models.Count()
            };
        }

    }
}
