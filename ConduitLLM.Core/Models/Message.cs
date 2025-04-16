using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models;

/// <summary>
/// Represents a single message within a chat completion request or response.
/// </summary>
public class Message
{
    /// <summary>
    /// The role of the author of this message (e.g., "system", "user", "assistant").
    /// </summary>
    [JsonPropertyName("role")]
    public required string Role { get; set; }

    /// <summary>
    /// The contents of the message.
    /// </summary>
    [JsonPropertyName("content")]
    public required string Content { get; set; }

    /// <summary>
    /// The name of the author of this message. May contain a-z, A-Z, 0-9, and underscores, with a maximum length of 64 characters.
    /// (Optional, primarily for function calling responses)
    /// </summary>
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    // Consider adding properties for function/tool calls if needed later
    // [JsonPropertyName("tool_calls")]
    // public List<ToolCall>? ToolCalls { get; set; }
    //
    // [JsonPropertyName("tool_call_id")]
    // public string? ToolCallId { get; set; }
}

/// <summary>
/// Defines standard roles for messages in a chat conversation.
/// </summary>
public static class MessageRole
{
    public const string System = "system";
    public const string User = "user";
    public const string Assistant = "assistant";
    public const string Tool = "tool"; // Role for tool/function call results
}
