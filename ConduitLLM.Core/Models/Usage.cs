using System.Text.Json;
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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PromptTokens { get; set; }

    /// <summary>
    /// Number of tokens in the generated completion.
    /// </summary>
    [JsonPropertyName("completion_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CompletionTokens { get; set; }

    /// <summary>
    /// Total number of tokens used in the request (prompt + completion).
    /// </summary>
    [JsonPropertyName("total_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TotalTokens { get; set; }

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
    /// Resolution for image generation (e.g., "1024x1024", "1792x1024").
    /// </summary>
    /// <remarks>
    /// Used to apply resolution-based multipliers to image generation costs.
    /// Format is typically "widthxheight" in pixels.
    /// </remarks>
    [JsonPropertyName("image_resolution")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ImageResolution { get; set; }

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

    /// <summary>
    /// Number of search units consumed (for rerank operations).
    /// </summary>
    /// <remarks>
    /// Used by reranking models like Cohere Rerank that charge per search unit.
    /// A search unit typically consists of 1 query + up to 100 documents to be ranked.
    /// </remarks>
    [JsonPropertyName("search_units")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SearchUnits { get; set; }

    /// <summary>
    /// Additional metadata for search operations.
    /// </summary>
    /// <remarks>
    /// Provides detailed breakdown of search/rerank operations for cost tracking and debugging.
    /// </remarks>
    [JsonPropertyName("search_metadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SearchUsageMetadata? SearchMetadata { get; set; }

    /// <summary>
    /// Number of inference steps used for image generation.
    /// </summary>
    /// <remarks>
    /// Used by providers like Fireworks that charge based on the number of iterative refinement steps.
    /// Each step represents one iteration of the diffusion process that refines noise into an image.
    /// Different models require different numbers of steps for optimal quality.
    /// Example: FLUX.1[schnell] uses 4 steps, SDXL typically uses 30 steps.
    /// </remarks>
    [JsonPropertyName("inference_steps")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? InferenceSteps { get; set; }

    /// <summary>
    /// Duration of audio in seconds (used for speech generation and transcription).
    /// </summary>
    /// <remarks>
    /// Used by audio models for both text-to-speech and speech-to-text operations.
    /// Most providers charge per minute of audio, which is calculated from this value.
    /// </remarks>
    [JsonPropertyName("audio_duration_seconds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? AudioDurationSeconds { get; set; }

    /// <summary>
    /// Optional metadata for provider-specific usage information.
    /// </summary>
    /// <remarks>
    /// Can include provider-specific details such as:
    /// - Cache TTL information
    /// - Model-specific parameters
    /// - Additional breakdown of resource usage
    /// - Provider-specific billing details
    /// </remarks>
    [JsonPropertyName("metadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Extension data to capture additional provider-specific fields not defined in the model.
    /// </summary>
    /// <remarks>
    /// This property captures any JSON properties that don't map to defined properties.
    /// For example, SambaNova includes performance metrics like completion_tokens_per_sec,
    /// time_to_first_token, total_latency, etc. These will be captured here without
    /// causing deserialization errors.
    /// </remarks>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>
/// Represents detailed usage metadata for search and reranking operations.
/// </summary>
public class SearchUsageMetadata
{
    /// <summary>
    /// Number of queries processed.
    /// </summary>
    [JsonPropertyName("query_count")]
    public int QueryCount { get; set; }

    /// <summary>
    /// Total number of documents ranked.
    /// </summary>
    [JsonPropertyName("document_count")]
    public int DocumentCount { get; set; }

    /// <summary>
    /// Number of documents that were split into chunks.
    /// </summary>
    /// <remarks>
    /// Documents exceeding 500 tokens are typically split into chunks,
    /// with each chunk counting as a separate document for billing purposes.
    /// </remarks>
    [JsonPropertyName("chunked_document_count")]
    public int ChunkedDocumentCount { get; set; }
}
