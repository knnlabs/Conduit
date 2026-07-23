using System.Text.Json;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Models;
using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Gateway.Endpoints
{
    /// <summary>
    /// Controller for discovering model capabilities and provider features.
    /// Provides runtime discovery for virtual key holders to understand available models and their capabilities.
    /// </summary>
    public class DiscoveryEndpoints : GatewayEndpointHandlerBase
    {
        private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;
        private readonly IModelCapabilityService _modelCapabilityService;
        private readonly IVirtualKeyService _virtualKeyService;
        private readonly IDiscoveryCacheService _discoveryCacheService;

        /// <summary>
        /// Initializes the Discovery endpoint handler.
        /// </summary>
        public DiscoveryEndpoints(
            IDbContextFactory<ConduitDbContext> dbContextFactory,
            IModelCapabilityService modelCapabilityService,
            IVirtualKeyService virtualKeyService,
            IDiscoveryCacheService discoveryCacheService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<DiscoveryEndpoints> logger)
            : base(null, httpContextAccessor, logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _modelCapabilityService = modelCapabilityService ?? throw new ArgumentNullException(nameof(modelCapabilityService));
            _virtualKeyService = virtualKeyService ?? throw new ArgumentNullException(nameof(virtualKeyService));
            _discoveryCacheService = discoveryCacheService ?? throw new ArgumentNullException(nameof(discoveryCacheService));
        }

        /// <summary>
        /// Discovery is authentication-protected but not balance-protected. A valid key may
        /// inspect capabilities even when its group has no remaining balance.
        /// </summary>
        private async Task<IResult?> ValidateDiscoveryAccessAsync()
        {
            var virtualKeyValue = HttpContext.User.FindFirst("VirtualKey")?.Value;
            if (string.IsNullOrEmpty(virtualKeyValue))
            {
                return OpenAIError(
                    StatusCodes.Status401Unauthorized,
                    "Virtual key not found",
                    VirtualKeyValidationFailureCodes.MissingKey,
                    "authentication_error");
            }

            var validation = await _virtualKeyService.ValidateVirtualKeyForAuthenticationAsync(virtualKeyValue);
            if (validation.IsValid && validation.Key is not null)
            {
                return null;
            }

            return OpenAIError(
                validation.HttpStatusCode,
                validation.Reason ?? "Virtual key validation failed.",
                validation.FailureCode ?? VirtualKeyValidationFailureCodes.ValidationError,
                validation.HttpStatusCode == StatusCodes.Status401Unauthorized
                    ? "authentication_error"
                    : "server_error");
        }

        /// <summary>
        /// Gets all discovered models and their capabilities for authenticated virtual keys.
        /// </summary>
        /// <param name="capability">Optional capability filter (e.g., "video_generation", "vision")</param>
        /// <returns>List of models with their capabilities.</returns>
        public async Task<IResult> GetModels(string? capability = null)
        {
            if (await ValidateDiscoveryAccessAsync() is { } accessFailure)
            {
                return accessFailure;
            }

            // Build cache key based on capability filter
            var cacheKey = DiscoveryCacheService.BuildCacheKey(capability);

            // Try to get from cache first
            var cachedResult = await _discoveryCacheService.GetDiscoveryResultsAsync(cacheKey);
            if (cachedResult != null)
            {
                Logger.LogDebug("Returning cached discovery results for capability: {Capability}", LoggingSanitizer.S(capability ?? "all"));
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

            Logger.LogDebug("Found {Count} enabled model mappings for discovery (capability filter: {Capability})",
                modelMappings.Count, LoggingSanitizer.S(capability ?? "all"));

            var models = new List<JsonElement>();

            foreach (var mapping in modelMappings)
            {
                // Skip if model is missing
                if (mapping.ModelProviderTypeAssociation?.Model == null)
                {
                    Logger.LogWarning("Model mapping {ModelAlias} has no model data", LoggingSanitizer.S(mapping.ModelAlias));
                    continue;
                }

                var model = mapping.ModelProviderTypeAssociation.Model;
                var caps = ModelCapabilityResolver.Resolve(model, mapping.ModelProviderTypeAssociation);

                // Apply capability filter if specified
                if (!string.IsNullOrEmpty(capability))
                {
                    var capabilityKey = capability.Replace("-", "_").ToLowerInvariant();
                    bool hasCapability = capabilityKey switch
                    {
                        "chat" => caps.SupportsChat,
                        "streaming" or "chat_stream" => caps.SupportsStreaming,
                        "vision" => caps.SupportsVision,
                        "image_input" => caps.SupportsImageInput,
                        "video_input" => caps.SupportsVideoInput,
                        "audio_input" => caps.SupportsAudioInput,
                        "file_input" => caps.SupportsFileInput,
                        "video_understanding" => caps.SupportsVideoUnderstanding,
                        "video_generation" => caps.SupportsVideoGeneration,
                        "image_generation" => caps.SupportsImageGeneration,
                        "embeddings" => caps.SupportsEmbeddings,
                        "function_calling" => caps.SupportsFunctionCalling,
                        "speech_to_text" or "audio_transcription" => caps.SupportsSpeechToText,
                        "text_to_speech" => caps.SupportsTextToSpeech,
                        "rerank" => caps.SupportsRerank,
                        _ => false
                    };

                    if (!hasCapability)
                    {
                        continue;
                    }
                }

                // Use overrides from association first, then fall back to model defaults
                var maxInputTokens = mapping.ModelProviderTypeAssociation.MaxInputTokens ?? model.MaxInputTokens ?? 0;
                var maxOutputTokens = mapping.ModelProviderTypeAssociation.MaxOutputTokens ?? model.MaxOutputTokens ?? 0;

                // Serialize to JsonElement for cache-safe storage (anonymous objects can't round-trip through JSON deserialization)
                models.Add(JsonSerializer.SerializeToElement(new
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
                    tokenizer_type = model.TokenizerType.ToString().ToLowerInvariant(),
                    input_modalities = caps.InputModalities,
                    output_modalities = caps.OutputModalities,
                    capability_source = caps.Source.ToString().ToLowerInvariant(),
                    capabilities_last_verified_at = caps.LastVerifiedAt,

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
                        image_input = caps.SupportsImageInput,
                        video_input = caps.SupportsVideoInput,
                        audio_input = caps.SupportsAudioInput,
                        file_input = caps.SupportsFileInput,
                        video_understanding = caps.SupportsVideoUnderstanding,
                        speech_to_text = caps.SupportsSpeechToText,
                        text_to_speech = caps.SupportsTextToSpeech,
                        rerank = caps.SupportsRerank,
                        function_calling = caps.SupportsFunctionCalling,
                        tool_use = caps.SupportsFunctionCalling, // Same as function calling for now
                        json_mode = false, // Not yet tracked
                        max_tokens = maxInputTokens + maxOutputTokens,
                        max_output_tokens = maxOutputTokens
                    }
                }));
            }

            // Cache the results for future requests
            var discoveryResult = new DiscoveryModelsResult
            {
                Data = models,
                Count = models.Count,
                CapabilityFilter = capability
            };

            await _discoveryCacheService.SetDiscoveryResultsAsync(cacheKey, discoveryResult);

            Logger.LogInformation("Cached discovery results for capability: {Capability} with {Count} models",
                LoggingSanitizer.S(capability ?? "all"), models.Count);

            return Ok(new
            {
                data = models,
                count = models.Count
            });
        }

        /// <summary>
        /// Gets all available capabilities in the system.
        /// </summary>
        /// <returns>List of all available capabilities.</returns>
        public async Task<IResult> GetCapabilities()
        {
            await Task.CompletedTask;

            // Return all known capabilities
            var capabilities = new[]
            {
                "chat",
                "chat_stream",
                "vision",
                "video_generation",
                "image_generation",
                "embeddings",
                "speech_to_text",
                "text_to_speech",
                "rerank",
                "function_calling",
                "tool_use",
                "json_mode"
            };

            return Ok(new
            {
                capabilities = capabilities
            });
        }

        /// <summary>
        /// Gets UI parameters for a specific model to enable dynamic UI generation.
        /// </summary>
        /// <param name="model">The model alias or identifier to get parameters for</param>
        /// <returns>JSON object containing UI parameter definitions for the model.</returns>
        public async Task<IResult> GetModelParameters(string model)
        {
            if (await ValidateDiscoveryAccessAsync() is { } accessFailure)
            {
                return accessFailure;
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
                return OpenAIError(404, $"Model '{model}' not found or has no parameter information", "model_not_found");
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
                    Logger.LogWarning(ex, "Failed to parse parameters for model {Model}", LoggingSanitizer.S(model));
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

        /// <summary>
        /// Gets all available function configurations for authenticated virtual keys.
        /// </summary>
        /// <param name="purpose">Optional purpose filter (e.g., "Search", "Answer", "RAG_Search")</param>
        /// <param name="providerType">Optional provider type filter (e.g., "Exa", "Perplexity")</param>
        /// <returns>List of available function configurations</returns>
        public async Task<IResult> GetFunctions(
            string? purpose = null,
            string? providerType = null)
        {
            if (await ValidateDiscoveryAccessAsync() is { } accessFailure)
            {
                return accessFailure;
            }

            // Build cache key based on filters
            var cacheKey = $"functions_discovery_{purpose ?? "all"}_{providerType ?? "all"}";

            // Try to get from cache first
            var cachedResult = await _discoveryCacheService.GetDiscoveryResultsAsync(cacheKey);
            if (cachedResult != null)
            {
                Logger.LogDebug("Returning cached function discovery results");
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
                Data = new List<JsonElement> { JsonSerializer.SerializeToElement(result) },
                Count = result.Count,
                CapabilityFilter = purpose
            };

            await _discoveryCacheService.SetDiscoveryResultsAsync(cacheKey, discoveryResult);

            Logger.LogInformation("Cached function discovery results with {Count} functions", result.Count);

            return Ok(result);
        }

        /// <summary>
        /// Gets parameter schema for a specific function configuration.
        /// Enables dynamic UI generation for function execution.
        /// </summary>
        /// <param name="functionConfigurationId">The function configuration ID</param>
        /// <returns>JSON schema defining required and optional parameters</returns>
        public async Task<IResult> GetFunctionParameters(int functionConfigurationId)
        {
            if (await ValidateDiscoveryAccessAsync() is { } accessFailure)
            {
                return accessFailure;
            }

            // Build cache key
            var cacheKey = $"function_parameters_{functionConfigurationId}";

            // Try to get from cache first
            var cachedResult = await _discoveryCacheService.GetDiscoveryResultsAsync(cacheKey);
            if (cachedResult != null)
            {
                Logger.LogDebug("Returning cached function parameter schema for config {ConfigId}", functionConfigurationId);
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
                return OpenAIError(404, $"Function configuration {functionConfigurationId} not found or is disabled", "not_found");
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
                    Logger.LogWarning(ex, "Failed to parse parameter schema for function config {ConfigId}", functionConfigurationId);
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
                Data = new List<JsonElement> { JsonSerializer.SerializeToElement(result) },
                Count = 1
            };

            await _discoveryCacheService.SetDiscoveryResultsAsync(cacheKey, discoveryResult);

            Logger.LogInformation("Cached function parameter schema for config {ConfigId}", functionConfigurationId);

            return Ok(result);
        }
    }
}
