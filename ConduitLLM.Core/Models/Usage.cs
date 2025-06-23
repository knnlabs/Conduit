using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models;

/// <summary>
/// Represents usage statistics for a chat completion request.
/// </summary>
public class Usage
{
    /// <summary>
    /// Number of tokens in the prompt.
    /// </summary>
    [JsonPropertyName("prompt_tokens")]
    public required int PromptTokens { get; set; }

    /// <summary>
    /// Number of tokens in the generated completion.
    /// </summary>
    [JsonPropertyName("completion_tokens")]
    public required int CompletionTokens { get; set; }

    /// <summary>
    /// Total number of tokens used in the request (prompt + completion).
    /// </summary>
    [JsonPropertyName("total_tokens")]
    public required int TotalTokens { get; set; }

    /// <summary>
    /// Number of images generated (used for image generation requests).
    /// </summary>
    [JsonPropertyName("image_count")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ImageCount { get; set; }

    /// <summary>
    /// Duration of video generated in seconds (used for video generation requests).
    /// </summary>
    [JsonPropertyName("video_duration_seconds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? VideoDurationSeconds { get; set; }

    /// <summary>
    /// Resolution of video generated (e.g., "1920x1080").
    /// </summary>
    [JsonPropertyName("video_resolution")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VideoResolution { get; set; }
}
