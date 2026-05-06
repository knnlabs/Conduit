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

            // Project to only the fields the loop reads — no full Provider/Model/Series graphs.
            // Inner Caps is null when the mapping is missing its Model row, which preserves
            // the existing "no model data" warning below.
            using var context = await _dbContextFactory.CreateDbContextAsync();

            var projections = await context.ModelProviderMappings
                .AsNoTracking()
                .Where(m => m.IsEnabled && m.Provider != null && m.Provider.IsEnabled)
                .Select(m => new
                {
                    m.ModelAlias,
                    Caps = m.ModelProviderTypeAssociation != null && m.ModelProviderTypeAssociation.Model != null
                        ? new
                        {
                            m.ModelProviderTypeAssociation.Model.SupportsChat,
                            m.ModelProviderTypeAssociation.Model.SupportsStreaming,
                            m.ModelProviderTypeAssociation.Model.SupportsVision,
                            m.ModelProviderTypeAssociation.Model.SupportsVideoGeneration,
                            m.ModelProviderTypeAssociation.Model.SupportsImageGeneration,
                            m.ModelProviderTypeAssociation.Model.SupportsEmbeddings,
                            m.ModelProviderTypeAssociation.Model.SupportsFunctionCalling,
                            m.ModelProviderTypeAssociation.Model.Description,
                            m.ModelProviderTypeAssociation.Model.ModelCardUrl,
                            m.ModelProviderTypeAssociation.Model.MaxInputTokens,
                            m.ModelProviderTypeAssociation.Model.MaxOutputTokens,
                            m.ModelProviderTypeAssociation.Model.TokenizerType
                        }
                        : null
                })
                .ToListAsync();

            var models = new List<DiscoveredModelDto>();

            foreach (var p in projections)
            {
                if (p.Caps == null)
                {
                    _logger.LogWarning("Model mapping {ModelAlias} has no model data", p.ModelAlias);
                    continue;
                }

                if (!string.IsNullOrEmpty(capability))
                {
                    var capabilityKey = capability.Replace("-", "_").ToLowerInvariant();
                    bool hasCapability = capabilityKey switch
                    {
                        "chat" => p.Caps.SupportsChat,
                        "streaming" or "chat_stream" => p.Caps.SupportsStreaming,
                        "vision" => p.Caps.SupportsVision,
                        "video_generation" => p.Caps.SupportsVideoGeneration,
                        "image_generation" => p.Caps.SupportsImageGeneration,
                        "embeddings" => p.Caps.SupportsEmbeddings,
                        "function_calling" => p.Caps.SupportsFunctionCalling,
                        _ => false
                    };

                    if (!hasCapability)
                    {
                        continue;
                    }
                }

                var capabilities = new Dictionary<string, object>
                {
                    ["supports_chat"] = p.Caps.SupportsChat,
                    ["supports_streaming"] = p.Caps.SupportsStreaming,
                    ["supports_vision"] = p.Caps.SupportsVision,
                    ["supports_function_calling"] = p.Caps.SupportsFunctionCalling,
                    ["supports_video_generation"] = p.Caps.SupportsVideoGeneration,
                    ["supports_image_generation"] = p.Caps.SupportsImageGeneration,
                    ["supports_embeddings"] = p.Caps.SupportsEmbeddings,
                    ["description"] = p.Caps.Description ?? "",
                    ["model_card_url"] = p.Caps.ModelCardUrl ?? "",
                    ["input_tokens"] = p.Caps.MaxInputTokens ?? 0,
                    ["output_tokens"] = p.Caps.MaxOutputTokens ?? 0,
                    ["tokenizer_type"] = p.Caps.TokenizerType.ToString().ToLowerInvariant()
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