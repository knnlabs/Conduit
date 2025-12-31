using System.Text.Json;
using System.Text.Json.Nodes;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Functions.Enums;
using ConduitLLM.Functions.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services;

/// <summary>
/// Service for discovering function configurations and converting them to LLM-compatible Tool definitions.
/// Supports caching via IFunctionDiscoveryCacheService for improved performance.
/// </summary>
public class FunctionDiscoveryService : IFunctionDiscoveryService
{
    private readonly IFunctionConfigurationRepository _functionConfigRepository;
    private readonly IFunctionDiscoveryCacheService? _cacheService;
    private readonly ILogger<FunctionDiscoveryService> _logger;

    public FunctionDiscoveryService(
        IFunctionConfigurationRepository functionConfigRepository,
        IFunctionDiscoveryCacheService? cacheService,
        ILogger<FunctionDiscoveryService> logger)
    {
        _functionConfigRepository = functionConfigRepository ?? throw new ArgumentNullException(nameof(functionConfigRepository));
        _cacheService = cacheService; // Nullable - caching is optional
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<Tool>> GetToolsForFunctionConfigurationsAsync(
        List<int> functionConfigurationIds,
        int virtualKeyId,
        CancellationToken cancellationToken = default)
    {
        if (functionConfigurationIds == null || functionConfigurationIds.Count == 0)
        {
            return new List<Tool>();
        }

        _logger.LogDebug("Loading {Count} function configurations for virtual key {VirtualKeyId}",
            functionConfigurationIds.Count, virtualKeyId);

        // Try to get from cache if caching is enabled
        if (_cacheService != null)
        {
            var cachedTools = await _cacheService.GetCachedToolsAsync(functionConfigurationIds, cancellationToken);
            if (cachedTools != null)
            {
                _logger.LogDebug("Cache hit: Loaded {Count} tools from cache for virtual key {VirtualKeyId}",
                    cachedTools.Count, virtualKeyId);
                return cachedTools;
            }

            _logger.LogDebug("Cache miss: Loading {Count} function configurations from database for virtual key {VirtualKeyId}",
                functionConfigurationIds.Count, virtualKeyId);
        }

        // Load all configurations from database
        var configurations = await _functionConfigRepository.GetByIdsAsync(functionConfigurationIds, cancellationToken);

        // Validate all requested configurations were found
        if (configurations.Count != functionConfigurationIds.Count)
        {
            var missingIds = functionConfigurationIds.Except(configurations.Select(c => c.Id)).ToList();
            throw new ArgumentException($"Function configurations not found: {string.Join(", ", missingIds)}");
        }

        // Validate all are enabled
        var disabledConfigs = configurations.Where(c => !c.IsEnabled).ToList();
        if (disabledConfigs.Any())
        {
            var disabledIds = disabledConfigs.Select(c => c.Id).ToList();
            throw new ArgumentException($"Function configurations are disabled: {string.Join(", ", disabledIds)}");
        }

        // Convert to Tools
        var tools = new List<Tool>();
        foreach (var config in configurations)
        {
            try
            {
                var tool = ConvertConfigurationToTool(config);
                tools.Add(tool);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert function configuration {ConfigId} to Tool", config.Id);
                throw new InvalidOperationException($"Failed to convert function configuration {config.Id} to Tool", ex);
            }
        }

        // Cache the tools if caching is enabled
        if (_cacheService != null)
        {
            try
            {
                await _cacheService.SetCachedToolsAsync(functionConfigurationIds, tools, ttlMinutes: null, cancellationToken);
                _logger.LogDebug("Cached {Count} tools for function configurations: {ConfigIds}",
                    tools.Count, string.Join(", ", functionConfigurationIds));
            }
            catch (Exception ex)
            {
                // Log but don't fail if caching fails
                _logger.LogWarning(ex, "Failed to cache tools for function configurations: {ConfigIds}",
                    string.Join(", ", functionConfigurationIds));
            }
        }

        _logger.LogInformation("Loaded {Count} tools for virtual key {VirtualKeyId}: {ToolNames}",
            tools.Count, virtualKeyId, string.Join(", ", tools.Select(t => t.Function.Name)));

        return tools;
    }

    public async Task<Dictionary<string, int>> GetFunctionNameToIdMappingAsync(
        List<int> functionConfigurationIds,
        CancellationToken cancellationToken = default)
    {
        if (functionConfigurationIds == null || functionConfigurationIds.Count == 0)
        {
            return new Dictionary<string, int>();
        }

        var configurations = await _functionConfigRepository.GetByIdsAsync(functionConfigurationIds, cancellationToken);

        var mapping = new Dictionary<string, int>();
        foreach (var config in configurations)
        {
            var functionName = GetFunctionNameForConfiguration(config);
            mapping[functionName] = config.Id;
        }

        return mapping;
    }

    /// <summary>
    /// Converts a FunctionConfiguration entity to a Tool definition.
    /// </summary>
    private Tool ConvertConfigurationToTool(ConduitLLM.Functions.Entities.FunctionConfiguration config)
    {
        var functionName = GetFunctionNameForConfiguration(config);

        // Parse parameter schema if available
        JsonObject? parameters = null;
        if (!string.IsNullOrWhiteSpace(config.ParameterSchema))
        {
            try
            {
                parameters = JsonSerializer.Deserialize<JsonObject>(config.ParameterSchema);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse parameter schema for function configuration {ConfigId}. Using null parameters.", config.Id);
            }
        }

        return new Tool
        {
            Type = "function",
            Function = new FunctionDefinition
            {
                Name = functionName,
                Description = GetFunctionDescription(config),
                Parameters = parameters
            }
        };
    }

    /// <summary>
    /// Generates a function name for the LLM to call.
    /// Uses the configuration name, converted to snake_case for LLM compatibility.
    /// </summary>
    private string GetFunctionNameForConfiguration(ConduitLLM.Functions.Entities.FunctionConfiguration config)
    {
        // Convert configuration name to snake_case for LLM compatibility
        // Example: "Production Exa Search" -> "production_exa_search"
        var name = config.ConfigurationName
            .ToLowerInvariant()
            .Replace(" ", "_")
            .Replace("-", "_");

        // Remove any non-alphanumeric characters except underscores
        name = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());

        // Ensure it starts with a letter (LLM requirement)
        if (!char.IsLetter(name[0]))
        {
            name = "func_" + name;
        }

        // Truncate to 64 characters (LLM limit)
        if (name.Length > 64)
        {
            name = name.Substring(0, 64);
        }

        return name;
    }

    /// <summary>
    /// Generates a description for the function that helps the LLM understand when to use it.
    /// </summary>
    private string GetFunctionDescription(ConduitLLM.Functions.Entities.FunctionConfiguration config)
    {
        // Use provided description if available
        if (!string.IsNullOrWhiteSpace(config.Description))
        {
            return config.Description;
        }

        // Generate description based on provider type and purpose
        var providerName = GetProviderDisplayName(config.ProviderType);
        var purposeDescription = GetPurposeDescription(config.Purpose);

        return $"{providerName} - {purposeDescription}";
    }

    /// <summary>
    /// Gets a human-readable name for the provider type.
    /// </summary>
    private string GetProviderDisplayName(FunctionProviderType providerType)
    {
        return providerType switch
        {
            FunctionProviderType.Exa => "Exa.ai Search",
            FunctionProviderType.Tavily => "Tavily Search",
            _ => providerType.ToString()
        };
    }

    /// <summary>
    /// Gets a description of what the function purpose means.
    /// </summary>
    private string GetPurposeDescription(FunctionPurpose purpose)
    {
        return purpose switch
        {
            FunctionPurpose.Search => "Search the web for information",
            FunctionPurpose.Answer => "Get direct answers to questions",
            FunctionPurpose.ContentRetrieval => "Retrieve and extract content from web pages",
            FunctionPurpose.RAG => "Search and retrieve information for RAG (Retrieval-Augmented Generation)",
            _ => purpose.ToString()
        };
    }
}
