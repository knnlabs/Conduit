using ConduitLLM.Functions.Enums;

namespace ConduitLLM.Functions.DTOs;

/// <summary>
/// DTO for listing available functions to end users
/// Simplified view suitable for Core API
/// </summary>
public class AvailableFunctionDto
{
    /// <summary>
    /// Function configuration ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Function name (suitable for API calls)
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Provider type
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Function purpose
    /// </summary>
    public string Purpose { get; set; } = string.Empty;

    /// <summary>
    /// Description of what this function does
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Execution mode (synchronous or asynchronous)
    /// </summary>
    public string ExecutionMode { get; set; } = string.Empty;

    /// <summary>
    /// Parameter schema (JSON Schema format)
    /// Describes what parameters this function accepts
    /// </summary>
    public object? Parameters { get; set; }
}
