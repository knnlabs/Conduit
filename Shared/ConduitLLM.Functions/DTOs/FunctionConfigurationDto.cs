using ConduitLLM.Functions.Enums;
using System.Text.Json;

namespace ConduitLLM.Functions.DTOs;

/// <summary>
/// DTO for function configuration information
/// </summary>
public class FunctionConfigurationDto
{
    public int Id { get; set; }
    public FunctionProviderType ProviderType { get; set; }
    public required string ConfigurationName { get; set; }
    public FunctionPurpose Purpose { get; set; }
    public ExecutionMode DefaultExecutionMode { get; set; }
    public string? BaseUrl { get; set; }
    public bool IsEnabled { get; set; }
    public int? CacheTtlMinutes { get; set; }
    public int? TimeoutSeconds { get; set; }
    public int? MaxRetries { get; set; }
    public Dictionary<string, JsonElement>? ProviderSettings { get; set; }
    public Dictionary<string, JsonElement>? ParameterSchema { get; set; }
    public string? Description { get; set; }
    public int CredentialCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
