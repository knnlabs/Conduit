using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using ConduitLLM.Core.Models;

namespace ConduitLLM.Providers.InternalModels.OpenAIModels
{
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
        public List<object>? Tools { get; init; }

        [JsonPropertyName("tool_choice")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? ToolChoice { get; init; }

        [JsonPropertyName("response_format")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ResponseFormat? ResponseFormat { get; init; }

        [JsonPropertyName("seed")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Seed { get; init; }

        [JsonPropertyName("top_p")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? TopP { get; init; } // Nucleus sampling, between 0 and 1

        [JsonPropertyName("n")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? N { get; init; } // Number of completions to generate

        [JsonPropertyName("stop")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? Stop { get; init; } // Can be string or array of strings

        [JsonPropertyName("presence_penalty")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? PresencePenalty { get; init; } // Between -2.0 and 2.0

        [JsonPropertyName("frequency_penalty")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? FrequencyPenalty { get; init; } // Between -2.0 and 2.0

        [JsonPropertyName("logit_bias")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, float>? LogitBias { get; init; } // Map of token IDs to bias values

        [JsonPropertyName("user")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? User { get; init; } // Unique identifier for the end-user
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
        public List<object>? ToolCalls { get; init; }

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

        [JsonPropertyName("system_fingerprint")]
        public string? SystemFingerprint { get; init; }

        [JsonPropertyName("seed")]
        public int? Seed { get; init; }
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

        [JsonPropertyName("system_fingerprint")]
        public string? SystemFingerprint { get; init; }

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

    internal record ListModelsResponse
    {
        [JsonPropertyName("object")]
        public string Object { get; init; } = "list"; // Expected value

        [JsonPropertyName("data")]
        public required List<OpenAIModelData> Data { get; init; }
    }

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

    // --- Internal Models for Embeddings ---
    // See: https://platform.openai.com/docs/api-reference/embeddings/create

    internal record EmbeddingRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("input")]
        public required object Input { get; init; } // Can be string or array of strings

        [JsonPropertyName("encoding_format")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? EncodingFormat { get; init; } // Default is "float"

        [JsonPropertyName("dimensions")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Dimensions { get; init; }

        [JsonPropertyName("user")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? User { get; init; }
    }

    internal record EmbeddingResponse
    {
        [JsonPropertyName("object")]
        public string Object { get; init; } = "list";

        [JsonPropertyName("data")]
        public required List<EmbeddingDataItem> Data { get; init; }

        [JsonPropertyName("model")]
        public string? Model { get; init; }

        [JsonPropertyName("usage")]
        public required OpenAIUsage Usage { get; init; }
    }

    internal record EmbeddingDataItem
    {
        [JsonPropertyName("object")]
        public string Object { get; init; } = "embedding";

        [JsonPropertyName("embedding")]
        public required IReadOnlyList<float> Embedding { get; init; }

        [JsonPropertyName("index")]
        public int Index { get; init; }
    }

    // --- Internal Models for Image Generation ---
    // See: https://platform.openai.com/docs/api-reference/images/create

    internal record ImageGenerationRequest
    {
        [JsonPropertyName("model")]
        public string? Model { get; init; }

        [JsonPropertyName("prompt")]
        public required string Prompt { get; init; }

        [JsonPropertyName("n")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? N { get; init; }

        [JsonPropertyName("size")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Size { get; init; }

        [JsonPropertyName("quality")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Quality { get; init; }

        [JsonPropertyName("style")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Style { get; init; }

        [JsonPropertyName("response_format")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ResponseFormat { get; init; }

        [JsonPropertyName("user")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? User { get; init; }
    }

    internal record ImageGenerationResponse
    {
        [JsonPropertyName("created")]
        public long Created { get; init; }

        [JsonPropertyName("data")]
        public required List<ImageData> Data { get; init; }
    }

    internal record ImageData
    {
        [JsonPropertyName("url")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Url { get; init; }

        [JsonPropertyName("b64_json")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? B64Json { get; init; }

        [JsonPropertyName("revised_prompt")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? RevisedPrompt { get; init; }
    }

    internal record ResponseFormat
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = "text"; // Either "text" or "json_object"
    }
}
