using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using ConduitLLM.Configuration.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

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
        /// Gets all discovered models and their capabilities, filtered by virtual key permissions.
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

                // Get virtual key details to check allowed models
                var virtualKey = await _virtualKeyService.ValidateVirtualKeyAsync(virtualKeyValue);
                if (virtualKey == null)
                {
                    return Unauthorized(new ErrorResponseDto("Invalid virtual key"));
                }

                using var context = await _dbContextFactory.CreateDbContextAsync();
                
                // Get all enabled model mappings with their providers
                var modelMappings = await context.ModelProviderMappings
                    .Include(m => m.Provider)
                    .Where(m => m.IsEnabled && m.Provider != null && m.Provider.IsEnabled)
                    .ToListAsync();

                var models = new List<object>();

                foreach (var mapping in modelMappings)
                {
                    // Check if model is allowed for this virtual key
                    if (!IsModelAllowed(mapping.ModelAlias, virtualKey.AllowedModels))
                    {
                        continue;
                    }

                    // Build capabilities object with detailed metadata
                    var capabilities = new Dictionary<string, object>();

                    // Chat capabilities (assume all models support basic chat)
                    capabilities["chat"] = new { supported = true };
                    capabilities["chat_stream"] = new { supported = true };

                    // Vision capability
                    if (await _modelCapabilityService.SupportsVisionAsync(mapping.ModelAlias))
                    {
                        capabilities["vision"] = new { supported = true };
                    }

                    // Audio capabilities
                    if (await _modelCapabilityService.SupportsAudioTranscriptionAsync(mapping.ModelAlias))
                    {
                        var supportedLanguages = await _modelCapabilityService.GetSupportedLanguagesAsync(mapping.ModelAlias);
                        var supportedFormats = await _modelCapabilityService.GetSupportedFormatsAsync(mapping.ModelAlias);
                        
                        capabilities["audio_transcription"] = new
                        {
                            supported = true,
                            supported_languages = supportedLanguages,
                            supported_formats = supportedFormats
                        };
                    }

                    // Text-to-speech capability
                    if (await _modelCapabilityService.SupportsTextToSpeechAsync(mapping.ModelAlias))
                    {
                        var supportedVoices = await _modelCapabilityService.GetSupportedVoicesAsync(mapping.ModelAlias);
                        var supportedLanguages = await _modelCapabilityService.GetSupportedLanguagesAsync(mapping.ModelAlias);
                        
                        capabilities["text_to_speech"] = new
                        {
                            supported = true,
                            supported_voices = supportedVoices,
                            supported_languages = supportedLanguages
                        };
                    }

                    // Real-time audio capability
                    if (await _modelCapabilityService.SupportsRealtimeAudioAsync(mapping.ModelAlias))
                    {
                        capabilities["realtime_audio"] = new { supported = true };
                    }

                    // Video generation capability
                    if (await _modelCapabilityService.SupportsVideoGenerationAsync(mapping.ModelAlias))
                    {
                        var videoCapability = new Dictionary<string, object>
                        {
                            ["supported"] = true
                        };

                        // TODO: Extract video-specific metadata from model configuration when available
                        // For now, use default values for MiniMax video model
                        if (mapping.ModelAlias.Contains("video", StringComparison.OrdinalIgnoreCase))
                        {
                            videoCapability["max_duration_seconds"] = 6;
                            videoCapability["supported_resolutions"] = new[] { "720x480", "1280x720", "1920x1080", "720x1280", "1080x1920" };
                            videoCapability["supported_fps"] = new[] { 24, 30 };
                            videoCapability["supports_custom_styles"] = true;
                        }

                        capabilities["video_generation"] = videoCapability;
                    }

                    // Image generation capability
                    if (mapping.SupportsImageGeneration)
                    {
                        var imageCapability = new Dictionary<string, object>
                        {
                            ["supported"] = true
                        };

                        // TODO: Extract image-specific metadata from model configuration when available
                        // For now, use common image sizes
                        imageCapability["supported_sizes"] = new[] { "256x256", "512x512", "1024x1024", "1792x1024", "1024x1792" };

                        capabilities["image_generation"] = imageCapability;
                    }

                    // Apply capability filter if specified
                    if (!string.IsNullOrEmpty(capability))
                    {
                        var capabilityKey = capability.Replace("-", "_").ToLowerInvariant();
                        if (!capabilities.ContainsKey(capabilityKey))
                        {
                            continue;
                        }
                    }

                    models.Add(new
                    {
                        id = mapping.ModelAlias,
                        provider = mapping.Provider?.ProviderType.ToString().ToLowerInvariant(),
                        display_name = mapping.ModelAlias,
                        capabilities = capabilities
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

        /// <summary>
        /// Checks if a model is allowed for a virtual key based on the allowed models list.
        /// </summary>
        private bool IsModelAllowed(string requestedModel, string? allowedModels)
        {
            if (string.IsNullOrEmpty(allowedModels))
                return true; // No restrictions

            var allowedModelsList = allowedModels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // First check for exact match
            if (allowedModelsList.Any(m => string.Equals(m, requestedModel, StringComparison.OrdinalIgnoreCase)))
                return true;

            // Then check for wildcard/prefix matches
            foreach (var allowedModel in allowedModelsList)
            {
                // Handle wildcards like "gpt-4*" to match any GPT-4 model
                if (allowedModel.EndsWith("*", StringComparison.OrdinalIgnoreCase) && allowedModel.Length > 1)
                {
                    string prefix = allowedModel.Substring(0, allowedModel.Length - 1);
                    if (requestedModel.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }
    }

    // TODO: Add audit logging for discovery requests to track which virtual keys are querying model information
    // TODO: Consider adding pricing information to model discovery responses once pricing data is available in the system
}
