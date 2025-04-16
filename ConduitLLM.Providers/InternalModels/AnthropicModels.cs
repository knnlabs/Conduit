using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace ConduitLLM.Providers.InternalModels;

// Internal models mirroring Anthropic's /v1/messages structure
// See: https://docs.anthropic.com/claude/reference/messages_post

internal record AnthropicMessageRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("messages")]
    public required IEnumerable<AnthropicMessage> Messages { get; init; }

    [JsonPropertyName("max_tokens")]
    public required int MaxTokens { get; init; } // Required by Anthropic

    [JsonPropertyName("system")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SystemPrompt { get; init; }

    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? Temperature { get; init; }

    [JsonPropertyName("top_p")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? TopP { get; init; }

    [JsonPropertyName("top_k")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TopK { get; init; }

    [JsonPropertyName("stream")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Stream { get; init; }

    [JsonPropertyName("stop_sequences")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IEnumerable<string>? StopSequences { get; init; }

    // TODO: Add metadata if needed
}

internal record AnthropicMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; init; } // "user" or "assistant"

    [JsonPropertyName("content")]
    public required string Content { get; init; } // Simple text content for now
    // TODO: Support complex content (list of blocks) if needed later
}

internal record AnthropicMessageResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; } // e.g., "message"

    [JsonPropertyName("role")]
    public string? Role { get; init; } // Should be "assistant"

    [JsonPropertyName("model")]
    public string? Model { get; init; } // Actual model used

    [JsonPropertyName("content")]
    public required List<AnthropicContentBlock> Content { get; init; }

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; init; } // e.g., "end_turn", "max_tokens", "stop_sequence"

    [JsonPropertyName("stop_sequence")]
    public string? StopSequence { get; init; } // Present if stopped by a sequence

    [JsonPropertyName("usage")]
    public required AnthropicUsage Usage { get; init; }
}

internal record AnthropicContentBlock
{
    [JsonPropertyName("type")]
    public required string Type { get; init; } // e.g., "text"

    [JsonPropertyName("text")]
    public string? Text { get; init; }
    // TODO: Add other content block types if needed (e.g., "image")
}

internal record AnthropicUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; init; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; init; }
}

// For error responses
// See: https://docs.anthropic.com/claude/reference/errors
internal record AnthropicErrorResponse
{
    [JsonPropertyName("type")]
    public required string Type { get; init; } // Should be "error"

    [JsonPropertyName("error")]
    public required AnthropicErrorDetails Error { get; init; }
}

internal record AnthropicErrorDetails
{
     [JsonPropertyName("type")]
     public required string Type { get; init; } // e.g., "invalid_request_error", "api_error"

     [JsonPropertyName("message")]
     public required string Message { get; init; }
}


// --- Internal Models for Streaming Events ---
// See: https://docs.anthropic.com/claude/reference/messages-streaming

// Base structure for delta content
internal record AnthropicStreamDelta
{
    [JsonPropertyName("type")]
    public required string Type { get; init; } // e.g., "text_delta"

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    // Note: Anthropic might add other delta types in the future
}

// Structure for usage within message_delta event
internal record AnthropicStreamUsage
{
    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; init; }
}

// --- Specific Event Data Structures ---

// Data for 'message_start' event
internal record AnthropicMessageStartEvent
{
    [JsonPropertyName("type")]
    public required string Type { get; init; } // "message_start"

    [JsonPropertyName("message")]
    public required AnthropicMessageResponse Message { get; init; } // Contains initial message metadata (id, model, role, usage.input_tokens)
}

// Data for 'content_block_start' event
internal record AnthropicContentBlockStartEvent
{
    [JsonPropertyName("type")]
    public required string Type { get; init; } // "content_block_start"

    [JsonPropertyName("index")]
    public required int Index { get; init; } // Index of the content block

    [JsonPropertyName("content_block")]
    public required AnthropicContentBlock ContentBlock { get; init; } // Contains type ("text")
}

// Data for 'ping' event (usually empty)
internal record AnthropicPingEvent
{
     [JsonPropertyName("type")]
     public required string Type { get; init; } // "ping"
}

// Data for 'content_block_delta' event
internal record AnthropicContentBlockDeltaEvent
{
    [JsonPropertyName("type")]
    public required string Type { get; init; } // "content_block_delta"

    [JsonPropertyName("index")]
    public required int Index { get; init; } // Index of the content block

    [JsonPropertyName("delta")]
    public required AnthropicStreamDelta Delta { get; init; } // Contains the actual text change
}

// Data for 'content_block_stop' event
internal record AnthropicContentBlockStopEvent
{
    [JsonPropertyName("type")]
    public required string Type { get; init; } // "content_block_stop"

    [JsonPropertyName("index")]
    public required int Index { get; init; } // Index of the content block
}

// Data for 'message_delta' event
internal record AnthropicMessageDeltaEvent
{
    [JsonPropertyName("type")]
    public required string Type { get; init; } // "message_delta"

    [JsonPropertyName("delta")]
    public required AnthropicMessageDeltaDetails Delta { get; init; } // Contains stop_reason, stop_sequence

    [JsonPropertyName("usage")]
    public required AnthropicStreamUsage Usage { get; init; } // Contains output_tokens for this delta
}

internal record AnthropicMessageDeltaDetails
{
    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; init; }

    [JsonPropertyName("stop_sequence")]
    public string? StopSequence { get; init; }
}


// Data for 'message_stop' event (usually empty)
internal record AnthropicMessageStopEvent
{
     [JsonPropertyName("type")]
     public required string Type { get; init; } // "message_stop"
}

// Data for 'error' event (uses AnthropicErrorDetails defined earlier)
internal record AnthropicStreamErrorEvent
{
    [JsonPropertyName("type")]
    public required string Type { get; init; } // "error"

    [JsonPropertyName("error")]
    public required AnthropicErrorDetails Error { get; init; }
}
