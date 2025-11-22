using System.ComponentModel.DataAnnotations;
using ConduitLLM.Functions.Enums;

namespace ConduitLLM.Functions.DTOs;

/// <summary>
/// Request DTO for creating a new function configuration
/// </summary>
public class CreateFunctionConfigurationRequest
{
    /// <summary>
    /// Type of provider (Exa, Perplexity, etc.)
    /// </summary>
    [Required]
    public FunctionProviderType ProviderType { get; set; }

    /// <summary>
    /// User-friendly name for this configuration
    /// </summary>
    [Required]
    [MaxLength(200)]
    public required string ConfigurationName { get; set; }

    /// <summary>
    /// Purpose/use case for this function
    /// </summary>
    [Required]
    public FunctionPurpose Purpose { get; set; }

    /// <summary>
    /// Default execution mode (Synchronous or Asynchronous)
    /// </summary>
    public ExecutionMode DefaultExecutionMode { get; set; } = ExecutionMode.Synchronous;

    /// <summary>
    /// Optional custom base URL
    /// </summary>
    [MaxLength(500)]
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Whether this configuration is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Cache TTL in minutes for function discovery results (null = no caching for this function)
    /// When set, this function's tool definition will be cached for the specified duration.
    /// Requires global Functions.DiscoveryCacheEnabled setting to be true.
    /// </summary>
    [Range(1, 10080)] // 1 minute to 1 week
    public int? CacheTtlMinutes { get; set; }

    /// <summary>
    /// Maximum execution timeout in seconds
    /// </summary>
    [Range(1, 3600)]
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    [Range(0, 10)]
    public int? MaxRetries { get; set; } = 3;

    /// <summary>
    /// Provider-specific settings as JSON string
    /// </summary>
    public string? ProviderSettings { get; set; }

    /// <summary>
    /// JSON Schema defining parameter requirements for this function
    /// </summary>
    public string? ParameterSchema { get; set; }

    /// <summary>
    /// Optional description
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }
}
