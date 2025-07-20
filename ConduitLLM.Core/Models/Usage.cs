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

    /// <summary>
    /// Indicates whether this usage is for a batch processing request.
    /// </summary>
    /// <remarks>
    /// When true, batch processing discounts may be applied to the cost calculation.
    /// </remarks>
    [JsonPropertyName("is_batch")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsBatch { get; set; }

    /// <summary>
    /// Quality tier for image generation (e.g., "standard", "hd").
    /// </summary>
    /// <remarks>
    /// Used to apply quality-based multipliers to image generation costs.
    /// Different providers may use different quality tier names.
    /// </remarks>
    [JsonPropertyName("image_quality")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ImageQuality { get; set; }

    /// <summary>
    /// Number of cached input tokens used (read from cache).
    /// </summary>
    /// <remarks>
    /// For providers that support prompt caching (e.g., Anthropic Claude, Google Gemini),
    /// this represents tokens that were read from the cache rather than processed as new input.
    /// These tokens are typically charged at a much lower rate than regular input tokens.
    /// </remarks>
    [JsonPropertyName("cached_input_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CachedInputTokens { get; set; }

    /// <summary>
    /// Number of tokens written to the cache.
    /// </summary>
    /// <remarks>
    /// For providers that support prompt caching (e.g., Anthropic Claude, Google Gemini),
    /// this represents tokens that were written to the cache for future reuse.
    /// Cache write operations may have a different cost than regular input processing.
    /// </remarks>
    [JsonPropertyName("cached_write_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CachedWriteTokens { get; set; }
}
