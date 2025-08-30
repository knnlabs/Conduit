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
                .Where(m => m.IsEnabled && m.Provider != null && m.Provider.IsEnabled)
                .ToListAsync();

            var models = new List<DiscoveredModelDto>();

            foreach (var mapping in modelMappings)
            {
                // Skip if model is missing
                if (mapping.Model == null)
                {
                    _logger.LogWarning("Model mapping {ModelAlias} has no model data", mapping.ModelAlias);
                    continue;
                }

                var caps = mapping.Model;

                // Apply capability filter if specified
                if (!string.IsNullOrEmpty(capability))
                {
                    var capabilityKey = capability.Replace("-", "_").ToLowerInvariant();
                    bool hasCapability = capabilityKey switch
                    {
                        "chat" => caps.SupportsChat,
                        "streaming" or "chat_stream" => caps.SupportsStreaming,
                        "vision" => caps.SupportsVision,
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
                    ["supports_video_generation"] = caps.SupportsVideoGeneration,
                    ["supports_image_generation"] = caps.SupportsImageGeneration,
                    ["supports_embeddings"] = caps.SupportsEmbeddings
                };

                // Add metadata
                capabilities["description"] = mapping.Model.Description ?? "";
                capabilities["model_card_url"] = mapping.Model.ModelCardUrl ?? "";
                capabilities["max_tokens"] = mapping.Model.MaxOutputTokens; // TODO: Think about provider-specific overrides. Also, should we break down input and output token counts?
                capabilities["tokenizer_type"] = mapping.Model.TokenizerType.ToString().ToLowerInvariant();

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