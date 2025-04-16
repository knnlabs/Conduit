using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models;

/// <summary>
/// Represents a chunk of a streamed chat completion response.
/// </summary>
public class ChatCompletionChunk
{
    /// <summary>
    /// A unique identifier for the chat completion chunk.
    /// </summary>
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    /// <summary>
    /// A list of chat completion choices. Can contain more than one choice if n is greater than 1.
    /// </summary>
    [JsonPropertyName("choices")]
    public List<StreamingChoice> Choices { get; set; } = new();

    /// <summary>
    /// The Unix timestamp (in seconds) of when the chat completion chunk was created.
    /// </summary>
    [JsonPropertyName("created")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? Created { get; set; }

    /// <summary>
    /// The model to generate the completion.
    /// </summary>
    [JsonPropertyName("model")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Model { get; set; }

    /// <summary>
    /// The object type, which is always "chat.completion.chunk".
    /// </summary>
    [JsonPropertyName("object")]
    public string Object { get; set; } = "chat.completion.chunk"; // Default value

    // Note: Usage information is typically not included in chunks,
    // but might appear in the final chunk from some providers or need
    // to be requested separately after the stream finishes.
}
