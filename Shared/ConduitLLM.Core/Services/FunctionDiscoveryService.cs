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
/// </summary>
public class FunctionDiscoveryService : IFunctionDiscoveryService
{
    private readonly IFunctionConfigurationRepository _functionConfigRepository;
    private readonly ILogger<FunctionDiscoveryService> _logger;

    public FunctionDiscoveryService(
        IFunctionConfigurationRepository functionConfigRepository,
        ILogger<FunctionDiscoveryService> logger)
    {
        _functionConfigRepository = functionConfigRepository ?? throw new ArgumentNullException(nameof(functionConfigRepository));
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

        // Load all configurations
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
