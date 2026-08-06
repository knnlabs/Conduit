using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models.Responses;

/// <summary>An OpenAI-compatible response object for Conduit's stateless subset.</summary>
public sealed class ResponseObject
{
    [JsonPropertyName("background")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Background { get; init; }

    [JsonPropertyName("completed_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? CompletedAt { get; init; }

    [JsonPropertyName("conversation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Conversation { get; init; }

    [JsonPropertyName("created_at")]
    public required long CreatedAt { get; init; }

    [JsonPropertyName("error")]
    public required object? Error { get; init; }

    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("incomplete_details")]
    public required object? IncompleteDetails { get; init; }

    [JsonPropertyName("instructions")]
    public required object? Instructions { get; init; }

    [JsonPropertyName("max_output_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxOutputTokens { get; init; }

    [JsonPropertyName("max_tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxToolCalls { get; init; }

    [JsonPropertyName("metadata")]
    public required Dictionary<string, string> Metadata { get; init; }

    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("moderation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Moderation { get; init; }

    [JsonPropertyName("object")]
    public required string Object { get; init; } = "response";

    [JsonPropertyName("output")]
    public required List<ResponseOutputMessage> Output { get; init; }

    [JsonPropertyName("output_text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OutputText { get; init; }

    [JsonPropertyName("parallel_tool_calls")]
    public required bool ParallelToolCalls { get; init; } = true;

    [JsonPropertyName("previous_response_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PreviousResponseId { get; init; }

    [JsonPropertyName("prompt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Prompt { get; init; }

    [JsonPropertyName("prompt_cache_key")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PromptCacheKey { get; init; }

    [JsonPropertyName("prompt_cache_options")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? PromptCacheOptions { get; init; }

    [JsonPropertyName("prompt_cache_retention")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PromptCacheRetention { get; init; }

    [JsonPropertyName("reasoning")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Reasoning { get; init; }

    [JsonPropertyName("safety_identifier")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SafetyIdentifier { get; init; }

    [JsonPropertyName("service_tier")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ServiceTier { get; init; }

    [JsonPropertyName("status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Status { get; init; }

    [JsonPropertyName("temperature")]
    public required double? Temperature { get; init; }

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResponseTextConfiguration? Text { get; init; }

    [JsonPropertyName("tool_choice")]
    public required object ToolChoice { get; init; } = "auto";

    [JsonPropertyName("tools")]
    public required List<JsonElement> Tools { get; init; } = [];

    [JsonPropertyName("top_logprobs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TopLogprobs { get; init; }

    [JsonPropertyName("top_p")]
    public required double? TopP { get; init; }

    [JsonPropertyName("truncation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Truncation { get; init; }

    [JsonPropertyName("usage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResponseUsage? Usage { get; init; }

    [JsonPropertyName("user")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? User { get; init; }
}

public sealed class ResponseOutputMessage
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = "message";

    [JsonPropertyName("role")]
    public string Role { get; init; } = "assistant";

    [JsonPropertyName("content")]
    public required List<ResponseOutputText> Content { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }
}

public sealed class ResponseOutputText
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "output_text";

    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("annotations")]
    public List<JsonElement> Annotations { get; init; } = [];

    [JsonPropertyName("logprobs")]
    public List<JsonElement> Logprobs { get; init; } = [];
}

public sealed class ResponseUsage
{
    [JsonPropertyName("input_tokens")]
    public required int InputTokens { get; init; }

    [JsonPropertyName("input_tokens_details")]
    public required ResponseInputTokenDetails InputTokensDetails { get; init; }

    [JsonPropertyName("output_tokens")]
    public required int OutputTokens { get; init; }

    [JsonPropertyName("output_tokens_details")]
    public required ResponseOutputTokenDetails OutputTokensDetails { get; init; }

    [JsonPropertyName("total_tokens")]
    public required int TotalTokens { get; init; }
}

public sealed class ResponseInputTokenDetails
{
    [JsonPropertyName("cached_tokens")]
    public required int CachedTokens { get; init; }

    [JsonPropertyName("cache_write_tokens")]
    public required int CacheWriteTokens { get; init; }
}

public sealed class ResponseOutputTokenDetails
{
    [JsonPropertyName("reasoning_tokens")]
    public required int ReasoningTokens { get; init; }
}

public sealed class ResponseTextConfiguration
{
    [JsonPropertyName("format")]
    public ResponseTextFormat Format { get; init; } = new();
}

public sealed class ResponseTextFormat
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "text";
}
