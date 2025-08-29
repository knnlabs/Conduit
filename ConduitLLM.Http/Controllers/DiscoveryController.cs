using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
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
        private readonly IDiscoveryCacheService _discoveryCacheService;
        private readonly ILogger<DiscoveryController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DiscoveryController"/> class.
        /// </summary>
        public DiscoveryController(
            IDbContextFactory<ConduitDbContext> dbContextFactory,
            IModelCapabilityService modelCapabilityService,
            IVirtualKeyService virtualKeyService,
            IDiscoveryCacheService discoveryCacheService,
            ILogger<DiscoveryController> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _modelCapabilityService = modelCapabilityService ?? throw new ArgumentNullException(nameof(modelCapabilityService));
            _virtualKeyService = virtualKeyService ?? throw new ArgumentNullException(nameof(virtualKeyService));
            _discoveryCacheService = discoveryCacheService ?? throw new ArgumentNullException(nameof(discoveryCacheService));
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

                // Build cache key based on capability filter
                var cacheKey = DiscoveryCacheService.BuildCacheKey(capability);
                
                // Try to get from cache first
                var cachedResult = await _discoveryCacheService.GetDiscoveryResultsAsync(cacheKey);
                if (cachedResult != null)
                {
                    _logger.LogDebug("Returning cached discovery results for capability: {Capability}", capability ?? "all");
                    return Ok(new
                    {
                        data = cachedResult.Data,
                        count = cachedResult.Count
                    });
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

                    // TODO: Revisit supported_parameters implementation after removing ApiParameters field
                    // Currently commented out as we're moving to full parameter pass-through
                    // and ApiParameters field is being deprecated. Parameters should be derived
                    // from the UI-focused Parameters JSON object instead.
                    /*
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
                    */

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
                        // supported_parameters = supportedParameters ?? Array.Empty<string>(), // TODO: Re-implement based on Parameters field
                        
                        // UI Parameters from Model or Series
                        parameters = mapping.Model?.ModelParameters ?? mapping.Model?.Series?.Parameters ?? "{}",
                        
                        // Capabilities (flat boolean flags)
                        supports_chat = caps.SupportsChat,
                        supports_streaming = caps.SupportsStreaming,
                        supports_vision = caps.SupportsVision,
                        supports_function_calling = caps.SupportsFunctionCalling,
                        supports_video_generation = caps.SupportsVideoGeneration,
                        supports_image_generation = caps.SupportsImageGeneration,
                        supports_embeddings = caps.SupportsEmbeddings
                        
                        // TODO: Future additions to consider:
                        // - context_window (from capabilities or series metadata)
                        // - training_cutoff date
                        // - pricing_tier or cost information
                        // - rate_limits
                        // - model_version
                    });
                }

                // Cache the results for future requests
                var discoveryResult = new DiscoveryModelsResult
                {
                    Data = models,
                    Count = models.Count,
                    CapabilityFilter = capability
                };
                
                await _discoveryCacheService.SetDiscoveryResultsAsync(cacheKey, discoveryResult);
                
                _logger.LogInformation("Cached discovery results for capability: {Capability} with {Count} models", 
                    capability ?? "all", models.Count);

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
        /// Gets UI parameters for a specific model to enable dynamic UI generation.
        /// </summary>
        /// <param name="model">The model alias or identifier to get parameters for</param>
        /// <returns>JSON object containing UI parameter definitions for the model.</returns>
        /// <remarks>
        /// This endpoint returns the UI-focused parameter definitions from the ModelSeries.Parameters field,
        /// which contains JSON objects defining sliders, selects, textareas, and other UI controls.
        /// This allows clients to dynamically generate appropriate UI controls without Admin API access.
        /// </remarks>
        [HttpGet("models/{model}/parameters")]
        public async Task<IActionResult> GetModelParameters(string model)
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
                
                // Find the model mapping by alias
                var modelMapping = await context.ModelProviderMappings
                    .Include(m => m.Model)
                        .ThenInclude(m => m!.Series)
                    .Where(m => m.ModelAlias == model && m.IsEnabled)
                    .FirstOrDefaultAsync();

                if (modelMapping == null)
                {
                    // Try to find by Model.Id if the input is numeric
                    if (int.TryParse(model, out var modelId))
                    {
                        modelMapping = await context.ModelProviderMappings
                            .Include(m => m.Model)
                                .ThenInclude(m => m!.Series)
                            .Where(m => m.ModelId == modelId && m.IsEnabled)
                            .FirstOrDefaultAsync();
                    }
                }

                if (modelMapping?.Model?.Series == null)
                {
                    return NotFound(new ErrorResponseDto($"Model '{model}' not found or has no parameter information"));
                }

                // Parse the Parameters JSON
                object? parameters = null;
                if (!string.IsNullOrEmpty(modelMapping.Model.Series.Parameters))
                {
                    try
                    {
                        parameters = System.Text.Json.JsonSerializer.Deserialize<object>(
                            modelMapping.Model.Series.Parameters);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse parameters for model {Model}", model);
                        parameters = new { };
                    }
                }

                return Ok(new
                {
                    model_id = modelMapping.ModelId,
                    model_alias = modelMapping.ModelAlias,
                    series_name = modelMapping.Model.Series.Name,
                    parameters = parameters ?? new { }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving model parameters for {Model}", model);
                return StatusCode(500, new ErrorResponseDto("Failed to retrieve model parameters"));
            }
        }

    }

    // TODO: Add audit logging for discovery requests to track which virtual keys are querying model information
    // TODO: Consider adding pricing information to model discovery responses once pricing data is available in the system
}
