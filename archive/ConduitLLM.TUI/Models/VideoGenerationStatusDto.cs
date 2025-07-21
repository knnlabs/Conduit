namespace ConduitLLM.TUI.Models;

/// <summary>
/// Represents the status update for a video generation task.
/// </summary>
public class VideoGenerationStatusDto
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
    /// Gets or sets the video URL when generation is complete.
    /// </summary>
    public string? VideoUrl { get; set; }

    /// <summary>
    /// Gets or sets the error message if the task failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets additional status message.
    /// </summary>
    public string? Message { get; set; }
}