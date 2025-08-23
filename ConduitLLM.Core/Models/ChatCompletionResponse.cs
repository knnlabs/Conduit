using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models;

/// <summary>
/// Represents a response from a chat completion request.
/// </summary>
public class ChatCompletionResponse
{
    /// <summary>
    /// A unique identifier for the chat completion.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    /// A list of chat completion choices. Can be more than one if n > 1.
    /// </summary>
    [JsonPropertyName("choices")]
    public required List<Choice> Choices { get; set; }

    /// <summary>
    /// The Unix timestamp (in seconds) of when the chat completion was created.
    /// </summary>
    [JsonPropertyName("created")]
    public required long Created { get; set; }

    /// <summary>
    /// The model used for the chat completion.
    /// </summary>
    [JsonPropertyName("model")]
    public required string Model { get; set; }

    /// <summary>
    /// This fingerprint represents the backend configuration that the model runs with.
    /// Can be used in conjunction with the seed request parameter to understand when backend changes have been made that might impact determinism.
    /// </summary>
    [JsonPropertyName("system_fingerprint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SystemFingerprint { get; set; }

    /// <summary>
    /// The object type, which is always "chat.completion".
    /// </summary>
    [JsonPropertyName("object")]
    public required string Object { get; set; } // Typically "chat.completion"

    /// <summary>
    /// Usage statistics for the completion request.
    /// </summary>
    [JsonPropertyName("usage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Usage? Usage { get; set; }

    /// <summary>
    /// The original model alias used in routing, if different from the model name.
    /// </summary>
    [JsonIgnore]
    public string? OriginalModelAlias { get; set; }

    /// <summary>
    /// The seed that was used for generation.
    /// </summary>
    [JsonPropertyName("seed")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Seed { get; set; }

    /// <summary>
    /// Performance metrics for this completion request.
    /// </summary>
    [JsonPropertyName("performance_metrics")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PerformanceMetrics? PerformanceMetrics { get; set; }
}
