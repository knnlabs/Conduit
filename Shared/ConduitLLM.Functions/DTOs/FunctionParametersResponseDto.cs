namespace ConduitLLM.Functions.DTOs;

/// <summary>
/// Response model for function parameter schema discovery.
/// Enables dynamic UI generation for function execution.
/// </summary>
public class FunctionParametersResponseDto
{
    /// <summary>
    /// The function configuration ID.
    /// </summary>
    public int FunctionConfigurationId { get; set; }

    /// <summary>
    /// User-friendly name for this configuration.
    /// </summary>
    public string ConfigurationName { get; set; } = string.Empty;

    /// <summary>
    /// Provider type (e.g., "Exa", "Perplexity").
    /// </summary>
    public string ProviderType { get; set; } = string.Empty;

    /// <summary>
    /// Function purpose (e.g., "Search", "Answer").
    /// </summary>
    public string Purpose { get; set; } = string.Empty;

    /// <summary>
    /// Parameter schema defining required and optional parameters.
    /// </summary>
    public object? ParameterSchema { get; set; }

    /// <summary>
    /// Example request demonstrating typical usage.
    /// </summary>
    public object? ExampleRequest { get; set; }
}
