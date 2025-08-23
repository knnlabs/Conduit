using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using ConduitLLM.Configuration.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Http.Controllers
{
    /// <summary>
    /// Controller for discovering model capabilities and provider features.
    /// Provides runtime discovery for virtual key holders to understand available models and their capabilities.
    /// </summary>
    [ApiController]
    [Route("v1/discovery")]
    [Authorize]
    public class DiscoveryController : ControllerBase
    {
        private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;
        private readonly IModelCapabilityService _modelCapabilityService;
        private readonly IVirtualKeyService _virtualKeyService;
        private readonly ILogger<DiscoveryController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DiscoveryController"/> class.
        /// </summary>
        public DiscoveryController(
            IDbContextFactory<ConduitDbContext> dbContextFactory,
            IModelCapabilityService modelCapabilityService,
            IVirtualKeyService virtualKeyService,
            ILogger<DiscoveryController> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _modelCapabilityService = modelCapabilityService ?? throw new ArgumentNullException(nameof(modelCapabilityService));
            _virtualKeyService = virtualKeyService ?? throw new ArgumentNullException(nameof(virtualKeyService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets all discovered models and their capabilities for authenticated virtual keys.
        /// </summary>
        /// <param name="capability">Optional capability filter (e.g., "video_generation", "vision")</param>
        /// <returns>List of models with their capabilities.</returns>
        [HttpGet("models")]
        public async Task<IActionResult> GetModels([FromQuery] string? capability = null)
        {
            try
            {
                // Get virtual key from user claims
                var virtualKeyValue = HttpContext.User.FindFirst("VirtualKey")?.Value;
                if (string.IsNullOrEmpty(virtualKeyValue))
                {
                    return Unauthorized(new ErrorResponseDto("Virtual key not found"));
                }

                // Validate virtual key is active
                var virtualKey = await _virtualKeyService.ValidateVirtualKeyAsync(virtualKeyValue);
                if (virtualKey == null)
                {
                    return Unauthorized(new ErrorResponseDto("Invalid virtual key"));
                }

                using var context = await _dbContextFactory.CreateDbContextAsync();
                
                // Get all enabled model mappings with their related data
                var modelMappings = await context.ModelProviderMappings
                    .Include(m => m.Provider)
                    .Include(m => m.Model)
                        .ThenInclude(m => m.Series)
                    .Include(m => m.Model)
                        .ThenInclude(m => m.Capabilities)
                    .Where(m => m.IsEnabled && m.Provider != null && m.Provider.IsEnabled)
                    .ToListAsync();

                var models = new List<object>();

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

                    // Parse parameters from mapping (priority) or series (fallback)
                    string[]? supportedParameters = null;
                    var parametersJson = mapping.ApiParameters ?? mapping.Model?.Series?.Parameters;
                    if (!string.IsNullOrEmpty(parametersJson))
                    {
                        try
                        {
                            supportedParameters = System.Text.Json.JsonSerializer.Deserialize<string[]>(parametersJson);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse parameters for model {ModelAlias}", mapping.ModelAlias);
                        }
                    }

                    models.Add(new
                    {
                        // Identity
                        id = mapping.ModelAlias,
                        provider = mapping.Provider?.ProviderType.ToString().ToLowerInvariant(),
                        display_name = mapping.ModelAlias,
                        
                        // Metadata
                        description = mapping.Model?.Description ?? string.Empty,
                        model_card_url = mapping.Model?.ModelCardUrl ?? string.Empty,
                        max_tokens = caps.MaxTokens,
                        tokenizer_type = caps.TokenizerType.ToString().ToLowerInvariant(),
                        
                        // Configuration
                        supported_parameters = supportedParameters ?? Array.Empty<string>(),
                        
                        // Capabilities (flat boolean flags)
                        supports_chat = caps.SupportsChat,
                        supports_streaming = caps.SupportsStreaming,
                        supports_vision = caps.SupportsVision,
                        supports_function_calling = caps.SupportsFunctionCalling,
                        supports_audio_transcription = caps.SupportsAudioTranscription,
                        supports_text_to_speech = caps.SupportsTextToSpeech,
                        supports_realtime_audio = caps.SupportsRealtimeAudio,
                        supports_video_generation = caps.SupportsVideoGeneration,
                        supports_image_generation = caps.SupportsImageGeneration,
                        supports_embeddings = caps.SupportsEmbeddings
                        
                        // TODO: Future additions to consider:
                        // - context_window (from capabilities or series metadata)
                        // - training_cutoff date
                        // - pricing_tier or cost information
                        // - supported_languages (parsed from JSON)
                        // - supported_voices (for TTS models)
                        // - supported_formats (for audio models)
                        // - rate_limits
                        // - model_version
                    });
                }

                // TODO: Consider implementing caching for discovery results to improve performance

                return Ok(new
                {
                    data = models,
                    count = models.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving model discovery information");
                return StatusCode(500, new ErrorResponseDto("Failed to retrieve model discovery information"));
            }
        }

        /// <summary>
        /// Gets all available capabilities in the system.
        /// </summary>
        /// <returns>List of all available capabilities.</returns>
        [HttpGet("capabilities")]
        public Task<IActionResult> GetCapabilities()
        {
            try
            {
                // Return all known capabilities
                var capabilities = new[]
                {
                    "chat",
                    "chat_stream",
                    "vision",
                    "audio_transcription",
                    "text_to_speech",
                    "realtime_audio",
                    "video_generation",
                    "image_generation",
                    "embeddings",
                    "function_calling",
                    "tool_use",
                    "json_mode"
                };

                return Task.FromResult<IActionResult>(Ok(new
                {
                    capabilities = capabilities
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving capabilities list");
                return Task.FromResult<IActionResult>(StatusCode(500, new ErrorResponseDto("Failed to retrieve capabilities")));
            }
        }

    }

    // TODO: Add audit logging for discovery requests to track which virtual keys are querying model information
    // TODO: Consider adding pricing information to model discovery responses once pricing data is available in the system
}
