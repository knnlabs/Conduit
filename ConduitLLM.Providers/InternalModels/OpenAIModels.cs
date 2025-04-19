using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Providers.InternalModels;

// Internal models mirroring OpenAI's /v1/chat/completions structure
// See: https://platform.openai.com/docs/api-reference/chat/create

internal record OpenAIChatCompletionRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("messages")]
    public required IEnumerable<OpenAIMessage> Messages { get; init; }

    // Add other parameters as needed (temperature, max_tokens, etc.)
    // For simplicity, starting with the basics.
    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? Temperature { get; init; }

    [JsonPropertyName("max_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxTokens { get; init; }

    [JsonPropertyName("stream")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] // Don't include if false (default)
    public bool Stream { get; init; } = false;

    // Tool/function calling support
    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Tool>? Tools { get; init; }

    [JsonPropertyName("tool_choice")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? ToolChoice { get; init; }

    // TODO: Add other optional parameters like top_p, n, stop, presence_penalty, frequency_penalty, logit_bias, user
}

internal record OpenAIMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; init; } // "system", "user", "assistant", "tool"

    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Content { get; init; }

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ToolCall>? ToolCalls { get; init; }

    [JsonPropertyName("tool_call_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolCallId { get; init; }
}

internal record OpenAIChatCompletionResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("object")]
    public string? Object { get; init; } // e.g., "chat.completion"

    [JsonPropertyName("created")]
    public long? Created { get; init; } // Unix timestamp

    [JsonPropertyName("model")]
    public string? Model { get; init; } // Model used

    [JsonPropertyName("choices")]
    public required List<OpenAIChoice> Choices { get; init; }

    [JsonPropertyName("usage")]
    public OpenAIUsage? Usage { get; init; }

    // Optional fields like system_fingerprint
}

internal record OpenAIChoice
{
    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("message")]
    public required OpenAIMessage Message { get; init; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; } // e.g., "stop", "length", "tool_calls"

    // Optional logprobs field
}

internal record OpenAIUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }
}

// --- Internal Models for Streaming Chunks ---
// See: https://platform.openai.com/docs/api-reference/chat/streaming

internal record OpenAIChatCompletionChunk
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("object")]
    public string? Object { get; init; } // e.g., "chat.completion.chunk"

    [JsonPropertyName("created")]
    public long? Created { get; init; } // Unix timestamp

    [JsonPropertyName("model")]
    public string? Model { get; init; } // Model used

    [JsonPropertyName("choices")]
    public required List<OpenAIStreamingChoice> Choices { get; init; }

    // Usage is typically not included in chunks
}

internal record OpenAIStreamingChoice
{
    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("delta")]
    public required OpenAIDeltaContent Delta { get; init; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; } // e.g., "stop", "length", "tool_calls"

    // Optional logprobs field
}

internal record OpenAIDeltaContent
{
    [JsonPropertyName("role")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Role { get; init; } // Usually only present in the first chunk for a choice

    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Content { get; init; } // The actual token(s)

    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ToolCallChunk>? ToolCalls { get; init; }
}

// --- Internal Models for Tool Calling ---
internal record Tool
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("metadata")]
    public JsonNode? Metadata { get; init; }

    [JsonPropertyName("availability")]
    public ToolAvailability? Availability { get; init; }
}

internal record ToolCall
{
    [JsonPropertyName("tool")]
    public required string Tool { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("user_message")]
    public string? UserMessage { get; init; }

    [JsonPropertyName("metadata")]
    public JsonNode? Metadata { get; init; }

    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }
}

internal record ToolCallChunk
{
    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("tool")]
    public required string Tool { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("user_message")]
    public string? UserMessage { get; init; }

    [JsonPropertyName("metadata")]
    public JsonNode? Metadata { get; init; }

    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }
}

internal record ToolAvailability
{
    [JsonPropertyName("availability")]
    public required string Availability { get; init; }
}

// --- Internal Models for Model Listing ---
// See: https://platform.openai.com/docs/api-reference/models/list

internal record OpenAIModelListResponse
{
    [JsonPropertyName("object")]
    public string Object { get; init; } = "list"; // Expected value

    [JsonPropertyName("data")]
    public required List<OpenAIModelData> Data { get; init; }
}

internal record OpenAIModelData
{
    [JsonPropertyName("id")]
    public required string Id { get; init; } // The model ID

    [JsonPropertyName("object")]
    public string Object { get; init; } = "model"; // Expected value

    [JsonPropertyName("created")]
    public long Created { get; init; } // Unix timestamp

    [JsonPropertyName("owned_by")]
    public required string OwnedBy { get; init; } // e.g., "openai", "system", "user"
}
