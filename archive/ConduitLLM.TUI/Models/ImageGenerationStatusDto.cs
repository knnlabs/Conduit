namespace ConduitLLM.TUI.Models;

/// <summary>
/// Represents the status update for an image generation task.
/// </summary>
public class ImageGenerationStatusDto
{
    /// <summary>
    /// Gets or sets the task ID.
    /// </summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current status of the task.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the progress percentage (0-100).
    /// </summary>
    public double Progress { get; set; }

    /// <summary>
    /// Gets or sets the list of generated image URLs.
    /// </summary>
    public List<string> ImageUrls { get; set; } = new();

    /// <summary>
    /// Gets or sets the error message if the task failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets additional status message.
    /// </summary>
    public string? Message { get; set; }
}