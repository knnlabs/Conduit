using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace ConduitLLM.Providers.InternalModels;

// Internal models mirroring Cohere's /v1/chat structure
// See: https://docs.cohere.com/reference/chat

internal record CohereChatRequest
{
    [JsonPropertyName("message")]
    public required string Message { get; init; } // The current user message

    [JsonPropertyName("model")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Model { get; init; } // Optional, defaults to command-r if not specified

    [JsonPropertyName("chat_history")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IEnumerable<CohereMessage>? ChatHistory { get; init; }

    [JsonPropertyName("preamble")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Preamble { get; init; } // System prompt equivalent

    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? Temperature { get; init; } // Defaults to 0.3

    [JsonPropertyName("max_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxTokens { get; init; }

    [JsonPropertyName("k")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? K { get; init; } // Top-K sampling, defaults to 0

    [JsonPropertyName("p")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? P { get; init; } // Top-P (nucleus) sampling, defaults to 0.75

    [JsonPropertyName("stop_sequences")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IEnumerable<string>? StopSequences { get; init; }

    [JsonPropertyName("stream")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] // Default is false
    public bool Stream { get; init; } = false;

    // TODO: Add other parameters like frequency_penalty, presence_penalty, seed, connectors, documents, tools if needed
}

internal record CohereMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; init; } // "USER" or "CHATBOT"

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    // TODO: Add tool_calls if implementing tools
}

// Response for non-streaming chat
internal record CohereChatResponse
{
    [JsonPropertyName("text")]
    public required string Text { get; init; } // The generated response text

    [JsonPropertyName("generation_id")]
    public string? GenerationId { get; init; }

    [JsonPropertyName("chat_history")]
    public List<CohereMessage>? ChatHistory { get; init; } // Updated history including the response

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; } // e.g., COMPLETE, MAX_TOKENS, ERROR, ERROR_TOXIC

    [JsonPropertyName("meta")]
    public CohereApiMeta? Meta { get; init; }

    // TODO: Add tool_calls, documents, citations if needed
}

internal record CohereApiMeta
{
    [JsonPropertyName("api_version")]
    public CohereApiVersionInfo? ApiVersion { get; init; }

    [JsonPropertyName("billed_units")]
    public CohereApiMetaBilledUnits? BilledUnits { get; init; }

    [JsonPropertyName("tokens")]
    public CohereApiMetaTokens? Tokens { get; init; } // Newer field, might replace billed_units
}

internal record CohereApiVersionInfo
{
    [JsonPropertyName("version")]
    public string? Version { get; init; }
}

internal record CohereApiMetaBilledUnits
{
    [JsonPropertyName("input_tokens")]
    public int? InputTokens { get; init; }

    [JsonPropertyName("output_tokens")]
    public int? OutputTokens { get; init; }
}

// Newer token count structure
internal record CohereApiMetaTokens
{
    [JsonPropertyName("input_tokens")]
    public int? InputTokens { get; init; }

    [JsonPropertyName("output_tokens")]
    public int? OutputTokens { get; init; }
}


// For Error Responses
// Structure might vary, this is a common pattern
// See: https://docs.cohere.com/reference/errors
internal record CohereErrorResponse
{
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    // Other potential fields: block_type, block_reason etc.
}


// --- Internal Models for Streaming Events ---
// See: https://docs.cohere.com/reference/streaming-chat

// Base structure for all streaming events
internal record CohereStreamEventBase
{
    [JsonPropertyName("event_type")]
    public required string EventType { get; init; }
}

// Event: stream-start
internal record CohereStreamStartEvent : CohereStreamEventBase
{
    [JsonPropertyName("generation_id")]
    public string? GenerationId { get; init; }
}

// Event: text-generation
internal record CohereTextGenerationEvent : CohereStreamEventBase
{
    [JsonPropertyName("text")]
    public required string Text { get; init; } // The chunk of generated text
}

// Event: citation-generation (Ignoring for now)
// internal record CohereCitationGenerationEvent : CohereStreamEventBase { ... }

// Event: tool-calls-generation (Ignoring for now)
// internal record CohereToolCallsGenerationEvent : CohereStreamEventBase { ... }

// Event: stream-end
internal record CohereStreamEndEvent : CohereStreamEventBase
{
    [JsonPropertyName("finish_reason")]
    public required string FinishReason { get; init; } // e.g., COMPLETE, MAX_TOKENS, ERROR, ERROR_TOXIC

    // The final response object is included in stream-end
    [JsonPropertyName("response")]
    public required CohereChatResponse Response { get; init; }
}

// Event: error (Uses CohereErrorResponse defined earlier)
// The stream might just terminate with an HTTP error, or potentially send an error event.
// Assuming standard HTTP error handling is primary for now.
