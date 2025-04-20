using System.Collections.Generic;
using System.Text.Json.Serialization; // Required for JsonPropertyName

namespace ConduitLLM.Providers.InternalModels;

// Based on https://github.com/ollama/ollama/blob/main/docs/api.md (as of early 2024)

// --- Chat Completions ---

public record OllamaMessage
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    // Optional: For multimodal models
    [JsonPropertyName("images")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Images { get; init; } // List of base64-encoded images
}

public record OllamaChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<OllamaMessage> Messages { get; init; } = new();

    [JsonPropertyName("format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Format { get; init; } // e.g., "json"

    [JsonPropertyName("options")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OllamaOptions? Options { get; init; }

    [JsonPropertyName("stream")]
    public bool Stream { get; init; } = false;

    [JsonPropertyName("keep_alive")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? KeepAlive { get; init; } // e.g., "5m"
}

public record OllamaChatResponse
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; init; } = string.Empty; // ISO 8601 format

    [JsonPropertyName("message")]
    public OllamaMessage? Message { get; init; }

    [JsonPropertyName("done")]
    public bool Done { get; init; }

    // Usage statistics (present if 'done' is true)
    [JsonPropertyName("total_duration")]
    public long? TotalDuration { get; init; } // Nanoseconds

    [JsonPropertyName("load_duration")]
    public long? LoadDuration { get; init; } // Nanoseconds

    [JsonPropertyName("prompt_eval_count")]
    public int? PromptEvalCount { get; init; }

    [JsonPropertyName("prompt_eval_duration")]
    public long? PromptEvalDuration { get; init; } // Nanoseconds

    [JsonPropertyName("eval_count")]
    public int? EvalCount { get; init; } // Completion tokens

    [JsonPropertyName("eval_duration")]
    public long? EvalDuration { get; init; } // Nanoseconds
}

// Represents a chunk in a streaming response
public record OllamaStreamChunk : OllamaChatResponse 
{
    // Inherits properties from OllamaChatResponse
    // The 'message' will contain the delta content
    // 'done' indicates the final chunk with usage stats
}


// --- Model Listing (/api/tags) ---

public record OllamaTagsResponse
{
    [JsonPropertyName("models")]
    public List<OllamaModelInfo> Models { get; init; } = new();
}

public record OllamaModelInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty; // e.g., "llama3:latest"

    [JsonPropertyName("modified_at")]
    public string ModifiedAt { get; init; } = string.Empty; // ISO 8601 format

    [JsonPropertyName("size")]
    public long Size { get; init; } // Bytes

    [JsonPropertyName("digest")]
    public string Digest { get; init; } = string.Empty;

    [JsonPropertyName("details")]
    public OllamaModelDetails? Details { get; init; }
}

public record OllamaModelDetails
{
    [JsonPropertyName("format")]
    public string Format { get; init; } = string.Empty;

    [JsonPropertyName("family")]
    public string Family { get; init; } = string.Empty;

    [JsonPropertyName("families")]
    public List<string>? Families { get; init; }

    [JsonPropertyName("parameter_size")]
    public string ParameterSize { get; init; } = string.Empty; // e.g., "7B"

    [JsonPropertyName("quantization_level")]
    public string QuantizationLevel { get; init; } = string.Empty; // e.g., "Q4_0"
}


// --- Embeddings (/api/embeddings) ---

public record OllamaEmbeddingRequest
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("prompt")]
    public string Prompt { get; init; } = string.Empty;

    [JsonPropertyName("options")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OllamaOptions? Options { get; init; }
}

public record OllamaEmbeddingResponse
{
    [JsonPropertyName("embedding")]
    public List<float> Embedding { get; init; } = new();
}


// --- Common Options ---

public record OllamaOptions
{
    // Add common Ollama parameters here as needed, e.g.:
    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? Temperature { get; init; }

    [JsonPropertyName("num_predict")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? NumPredict { get; init; } // Max tokens equivalent

    [JsonPropertyName("top_k")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TopK { get; init; }

    [JsonPropertyName("top_p")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? TopP { get; init; }

    [JsonPropertyName("stop")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Stop { get; init; }

    // Add other options as required: seed, mirostat, etc.
}
