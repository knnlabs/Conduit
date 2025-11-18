using System.ComponentModel.DataAnnotations;
using ConduitLLM.Functions.Enums;

namespace ConduitLLM.Functions.DTOs;

/// <summary>
/// Request DTO for updating an existing function configuration
/// </summary>
public class UpdateFunctionConfigurationRequest
{
    /// <summary>
    /// User-friendly name for this configuration
    /// </summary>
    [MaxLength(200)]
    public string? ConfigurationName { get; set; }

    /// <summary>
    /// Purpose/use case for this function
    /// </summary>
    public FunctionPurpose? Purpose { get; set; }

    /// <summary>
    /// Default execution mode
    /// </summary>
    public ExecutionMode? DefaultExecutionMode { get; set; }

    /// <summary>
    /// Optional custom base URL
    /// </summary>
    [MaxLength(500)]
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Whether this configuration is enabled
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// Maximum execution timeout in seconds
    /// </summary>
    [Range(1, 3600)]
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    [Range(0, 10)]
    public int? MaxRetries { get; set; }

    /// <summary>
    /// Provider-specific settings as JSON string
    /// </summary>
    public string? ProviderSettings { get; set; }

    /// <summary>
    /// Optional description
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }
}
