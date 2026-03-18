using ConduitLLM.Configuration;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using Microsoft.AspNetCore.Authorization;
using ConduitLLM.Configuration.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Gateway.Controllers
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
                    _logger.LogDebug("Returning cached discovery results for capability: {Capability}", LoggingSanitizer.S(capability ?? "all"));
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
                    .Include(m => m.ModelProviderTypeAssociation)
                        .ThenInclude(mpta => mpta.Model)
                            .ThenInclude(m => m.Series)
                    .AsNoTracking()
                    .Where(m => m.IsEnabled && m.Provider != null && m.Provider.IsEnabled)
                    .ToListAsync();
                
                _logger.LogDebug("Found {Count} enabled model mappings for discovery (capability filter: {Capability})",
                    modelMappings.Count, LoggingSanitizer.S(capability ?? "all"));

                var models = new List<object>();

                foreach (var mapping in modelMappings)
                {
                    // Skip if model is missing
                    if (mapping.ModelProviderTypeAssociation?.Model == null)
                    {
                        _logger.LogWarning("Model mapping {ModelAlias} has no model data", LoggingSanitizer.S(mapping.ModelAlias));
                        continue;
                    }

                    var caps = mapping.ModelProviderTypeAssociation.Model;

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
                    var parametersJson = mapping.ApiParameters ?? mapping.ModelProviderTypeAssociation?.Model?.Series?.Parameters;
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

                    // Use overrides from association first, then fall back to model defaults
                    var maxInputTokens = mapping.ModelProviderTypeAssociation.MaxInputTokens ?? caps.MaxInputTokens ?? 0;
                    var maxOutputTokens = mapping.ModelProviderTypeAssociation.MaxOutputTokens ?? caps.MaxOutputTokens ?? 0;

                    models.Add(new
                    {
                        // Identity
                        id = mapping.ModelAlias,
                        provider = mapping.Provider?.ProviderType.ToString().ToLowerInvariant(),
                        display_name = mapping.ModelAlias,
                        
                        // Metadata
                        description = mapping.ModelProviderTypeAssociation?.Model?.Description ?? string.Empty,
                        model_card_url = mapping.ModelProviderTypeAssociation?.Model?.ModelCardUrl ?? string.Empty,
                        max_tokens = maxInputTokens + maxOutputTokens, // Total context window size
                        max_input_tokens = maxInputTokens,
                        max_output_tokens = maxOutputTokens,
                        tokenizer_type = caps.TokenizerType.ToString().ToLowerInvariant(),
                        
                        // Configuration
                        // supported_parameters = supportedParameters ?? Array.Empty<string>(), // TODO: Re-implement based on Parameters field
                        
                        // UI Parameters from Model or Series
                        parameters = mapping.ModelProviderTypeAssociation?.Model?.ModelParameters ?? mapping.ModelProviderTypeAssociation?.Model?.Series?.Parameters ?? "{}",
                        
                        // Capabilities (nested object as expected by SDK)
                        capabilities = new
                        {
                            chat = caps.SupportsChat,
                            chat_stream = caps.SupportsStreaming,
                            embeddings = caps.SupportsEmbeddings,
                            image_generation = caps.SupportsImageGeneration,
                            vision = caps.SupportsVision,
                            video_generation = caps.SupportsVideoGeneration,
                            video_understanding = false, // Not yet supported
                            function_calling = caps.SupportsFunctionCalling,
                            tool_use = caps.SupportsFunctionCalling, // Same as function calling for now
                            json_mode = false, // Not yet tracked
                            max_tokens = maxInputTokens + maxOutputTokens,
                            max_output_tokens = maxOutputTokens
                        }
                        
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
                    LoggingSanitizer.S(capability ?? "all"), models.Count);

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
                    .Include(m => m.ModelProviderTypeAssociation)
                        .ThenInclude(mpta => mpta.Model)
                            .ThenInclude(m => m!.Series)
                    .AsNoTracking()
                    .Where(m => m.ModelAlias == model && m.IsEnabled)
                    .FirstOrDefaultAsync();

                if (modelMapping == null)
                {
                    // Try to find by Model.Id if the input is numeric
                    if (int.TryParse(model, out var modelId))
                    {
                        modelMapping = await context.ModelProviderMappings
                            .Include(m => m.ModelProviderTypeAssociation)
                                .ThenInclude(mpta => mpta.Model)
                                    .ThenInclude(m => m!.Series)
                            .AsNoTracking()
                            .Where(m => m.ModelProviderTypeAssociation != null && m.ModelProviderTypeAssociation.ModelId == modelId && m.IsEnabled)
                            .FirstOrDefaultAsync();
                    }
                }

                if (modelMapping?.ModelProviderTypeAssociation?.Model == null)
                {
                    return NotFound(new ErrorResponseDto($"Model '{model}' not found or has no parameter information"));
                }

                // Parse the Parameters JSON - check model-specific parameters first, then fall back to series
                object? parameters = null;
                var parametersJson = modelMapping.ModelProviderTypeAssociation.Model.ModelParameters 
                    ?? modelMapping.ModelProviderTypeAssociation.Model.Series?.Parameters;
                    
                if (!string.IsNullOrEmpty(parametersJson))
                {
                    try
                    {
                        parameters = System.Text.Json.JsonSerializer.Deserialize<object>(parametersJson);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse parameters for model {Model}", LoggingSanitizer.S(model));
                        parameters = new { };
                    }
                }

                return Ok(new
                {
                    model_id = modelMapping.ModelProviderTypeAssociation.ModelId,
                    model_alias = modelMapping.ModelAlias,
                    series_name = modelMapping.ModelProviderTypeAssociation.Model.Series?.Name ?? string.Empty,
                    parameters = parameters ?? new { }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving model parameters for {Model}", LoggingSanitizer.S(model));
                return StatusCode(500, new ErrorResponseDto("Failed to retrieve model parameters"));
            }
        }

        /// <summary>
        /// Gets all available function configurations for authenticated virtual keys.
        /// </summary>
        /// <param name="purpose">Optional purpose filter (e.g., "Search", "Answer", "RAG_Search")</param>
        /// <param name="providerType">Optional provider type filter (e.g., "Exa", "Perplexity")</param>
        /// <returns>List of available function configurations</returns>
        [HttpGet("functions")]
        public async Task<IActionResult> GetFunctions(
            [FromQuery] string? purpose = null,
            [FromQuery] string? providerType = null)
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

                // Build cache key based on filters
                var cacheKey = $"functions_discovery_{purpose ?? "all"}_{providerType ?? "all"}";

                // Try to get from cache first
                var cachedResult = await _discoveryCacheService.GetDiscoveryResultsAsync(cacheKey);
                if (cachedResult != null)
                {
                    _logger.LogDebug("Returning cached function discovery results");
                    return Ok(cachedResult.Data);
                }

                using var context = await _dbContextFactory.CreateDbContextAsync();

                // Get all enabled function configurations
                var query = context.FunctionConfigurations
                    .Where(fc => fc.IsEnabled);

                // Apply filters
                if (!string.IsNullOrEmpty(purpose))
                {
                    if (Enum.TryParse<ConduitLLM.Functions.Enums.FunctionPurpose>(purpose, true, out var purposeEnum))
                    {
                        query = query.Where(fc => fc.Purpose == purposeEnum);
                    }
                }

                if (!string.IsNullOrEmpty(providerType))
                {
                    if (Enum.TryParse<ConduitLLM.Functions.Enums.FunctionProviderType>(providerType, true, out var providerEnum))
                    {
                        query = query.Where(fc => fc.ProviderType == providerEnum);
                    }
                }

                var configurations = await query.AsNoTracking().ToListAsync();

                var result = new ConduitLLM.Functions.DTOs.FunctionDiscoveryResponse
                {
                    Functions = configurations.Select(fc => new ConduitLLM.Functions.DTOs.FunctionDiscoveryDto
                    {
                        Id = fc.Id,
                        ConfigurationName = fc.ConfigurationName,
                        ProviderType = fc.ProviderType.ToString(),
                        Purpose = fc.Purpose.ToString(),
                        Description = fc.Description,
                        DefaultExecutionMode = fc.DefaultExecutionMode.ToString(),
                        IsEnabled = fc.IsEnabled,
                        TimeoutSeconds = fc.TimeoutSeconds
                    }).ToList(),
                    Count = configurations.Count
                };

                // Cache the results
                var discoveryResult = new DiscoveryModelsResult
                {
                    Data = new List<object> { result },
                    Count = result.Count,
                    CapabilityFilter = purpose
                };

                await _discoveryCacheService.SetDiscoveryResultsAsync(cacheKey, discoveryResult);

                _logger.LogInformation("Cached function discovery results with {Count} functions", result.Count);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving function discovery information");
                return StatusCode(500, new ErrorResponseDto("Failed to retrieve function discovery information"));
            }
        }

        /// <summary>
        /// Gets parameter schema for a specific function configuration.
        /// Enables dynamic UI generation for function execution.
        /// </summary>
        /// <param name="functionConfigurationId">The function configuration ID</param>
        /// <returns>JSON schema defining required and optional parameters</returns>
        [HttpGet("functions/{functionConfigurationId}/parameters")]
        public async Task<IActionResult> GetFunctionParameters(int functionConfigurationId)
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

                // Build cache key
                var cacheKey = $"function_parameters_{functionConfigurationId}";

                // Try to get from cache first
                var cachedResult = await _discoveryCacheService.GetDiscoveryResultsAsync(cacheKey);
                if (cachedResult != null)
                {
                    _logger.LogDebug("Returning cached function parameter schema for config {ConfigId}", functionConfigurationId);
                    return Ok(cachedResult.Data);
                }

                using var context = await _dbContextFactory.CreateDbContextAsync();

                // Find the function configuration
                var configuration = await context.FunctionConfigurations
                    .AsNoTracking()
                    .Where(fc => fc.Id == functionConfigurationId && fc.IsEnabled)
                    .FirstOrDefaultAsync();

                if (configuration == null)
                {
                    return NotFound(new ErrorResponseDto($"Function configuration {functionConfigurationId} not found or is disabled"));
                }

                // Parse the parameter schema
                object? parameterSchema = null;
                object? exampleRequest = null;

                if (!string.IsNullOrEmpty(configuration.ParameterSchema))
                {
                    try
                    {
                        var schemaDoc = System.Text.Json.JsonDocument.Parse(configuration.ParameterSchema);
                        parameterSchema = System.Text.Json.JsonSerializer.Deserialize<object>(configuration.ParameterSchema);

                        // Try to extract example request if it's in the schema
                        if (schemaDoc.RootElement.TryGetProperty("example", out var exampleElement))
                        {
                            exampleRequest = System.Text.Json.JsonSerializer.Deserialize<object>(exampleElement.GetRawText());
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse parameter schema for function config {ConfigId}", functionConfigurationId);
                        parameterSchema = new { };
                    }
                }

                var result = new ConduitLLM.Functions.DTOs.FunctionParametersResponseDto
                {
                    FunctionConfigurationId = configuration.Id,
                    ConfigurationName = configuration.ConfigurationName,
                    ProviderType = configuration.ProviderType.ToString(),
                    Purpose = configuration.Purpose.ToString(),
                    ParameterSchema = parameterSchema ?? new { },
                    ExampleRequest = exampleRequest
                };

                // Cache the results
                var discoveryResult = new DiscoveryModelsResult
                {
                    Data = new List<object> { result },
                    Count = 1
                };

                await _discoveryCacheService.SetDiscoveryResultsAsync(cacheKey, discoveryResult);

                _logger.LogInformation("Cached function parameter schema for config {ConfigId}", functionConfigurationId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving function parameters for config {ConfigId}", functionConfigurationId);
                return StatusCode(500, new ErrorResponseDto("Failed to retrieve function parameters"));
            }
        }
    }

    // TODO: Add audit logging for discovery requests to track which virtual keys are querying model information
    // TODO: Consider adding pricing information to model discovery responses once pricing data is available in the system
}
