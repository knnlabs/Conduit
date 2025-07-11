namespace ConduitLLM.TUI.Models;

/// <summary>
/// Represents model capability information for the TUI.
/// </summary>
public class ModelCapabilityDto
{
    /// <summary>
    /// Gets or sets the model identifier.
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of capabilities.
    /// </summary>
    public List<string> Capabilities { get; set; } = new();

    /// <summary>
    /// Gets or sets whether the model is available.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Gets or sets the error message if the model is not available.
    /// </summary>
    public string? ErrorMessage { get; set; }
}