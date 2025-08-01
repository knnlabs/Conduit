using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ConduitLLM.Providers.Providers.Cohere.Models;

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

    [JsonPropertyName("frequency_penalty")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? FrequencyPenalty { get; init; } // Between 0.0 and 1.0

    [JsonPropertyName("presence_penalty")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? PresencePenalty { get; init; } // Between 0.0 and 1.0

    [JsonPropertyName("seed")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Seed { get; init; } // For deterministic generation

    [JsonPropertyName("connectors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IEnumerable<CohereConnector>? Connectors { get; init; } // For RAG support

    [JsonPropertyName("documents")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IEnumerable<CohereDocument>? Documents { get; init; } // For RAG support

    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IEnumerable<CohereTool>? Tools { get; init; } // For function calling
}

internal record CohereMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; init; } // "USER" or "CHATBOT"

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IEnumerable<CohereToolCall>? ToolCalls { get; init; } // For tool calling support
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

    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IEnumerable<CohereToolCall>? ToolCalls { get; init; } // Tool calls in the response

    [JsonPropertyName("documents")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IEnumerable<CohereDocument>? Documents { get; init; } // Documents used for RAG

    [JsonPropertyName("citations")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IEnumerable<CohereCitation>? Citations { get; init; } // Citations for attribution
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

// --- Tool and RAG Support Models ---

internal record CohereTool
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("parameters")]
    public object? Parameters { get; init; } // JSON schema for the tool parameters
}

internal record CohereToolCall
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("parameters")]
    public object? Parameters { get; init; } // The tool call parameters
}

internal record CohereConnector
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("options")]
    public object? Options { get; init; } // Connector-specific options
}

internal record CohereDocument
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }
}

internal record CohereCitation
{
    [JsonPropertyName("start")]
    public int Start { get; init; } // Start position in the generated text

    [JsonPropertyName("end")]
    public int End { get; init; } // End position in the generated text

    [JsonPropertyName("text")]
    public string? Text { get; init; } // The cited text

    [JsonPropertyName("document_ids")]
    public IEnumerable<string>? DocumentIds { get; init; } // IDs of source documents
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

// --- Internal Models for Embeddings ---
// See: https://docs.cohere.com/reference/embed

internal record CohereEmbedRequest
{
    [JsonPropertyName("texts")]
    public required List<string> Texts { get; init; }
    
    [JsonPropertyName("model")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Model { get; init; } // e.g., "embed-english-v3.0", "embed-multilingual-v3.0"
    
    [JsonPropertyName("input_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InputType { get; init; } // "search_document", "search_query", "classification", "clustering"
    
    [JsonPropertyName("truncate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Truncate { get; init; } // "NONE", "START", "END"
}

internal record CohereEmbedResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }
    
    [JsonPropertyName("embeddings")]
    public required List<List<float>> Embeddings { get; init; }
    
    [JsonPropertyName("meta")]
    public CohereApiMeta? Meta { get; init; }
}
