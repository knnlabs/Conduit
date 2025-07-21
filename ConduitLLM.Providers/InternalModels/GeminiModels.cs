using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ConduitLLM.Providers.InternalModels;

// Internal models mirroring Google Gemini API's generateContent structure
// See: https://ai.google.dev/api/rest/v1beta/models/generateContent

internal record GeminiGenerateContentRequest
{
    [JsonPropertyName("contents")]
    public required IEnumerable<GeminiContent> Contents { get; init; }

    [JsonPropertyName("generationConfig")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GeminiGenerationConfig? GenerationConfig { get; init; }

    [JsonPropertyName("safetySettings")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IEnumerable<GeminiSafetySetting>? SafetySettings { get; init; }

    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IEnumerable<GeminiTool>? Tools { get; init; } // For function calling support
}

internal record GeminiContent
{
    [JsonPropertyName("role")]
    public required string Role { get; init; } // "user" or "model"

    [JsonPropertyName("parts")]
    public required IEnumerable<GeminiPart> Parts { get; init; }
}

internal record GeminiPart
{
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("inline_data")]
    public GeminiInlineData? InlineData { get; init; }
}

internal record GeminiInlineData
{
    [JsonPropertyName("mime_type")]
    public required string MimeType { get; init; }

    [JsonPropertyName("data")]
    public required string Data { get; init; }
}

internal record GeminiGenerationConfig
{
    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? Temperature { get; init; }

    [JsonPropertyName("topP")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? TopP { get; init; }

    [JsonPropertyName("topK")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TopK { get; init; }

    [JsonPropertyName("candidateCount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CandidateCount { get; init; } // Equivalent to 'n' in OpenAI? Typically 1.

    [JsonPropertyName("maxOutputTokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxOutputTokens { get; init; } // Equivalent to 'max_tokens'

    [JsonPropertyName("stopSequences")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IEnumerable<string>? StopSequences { get; init; }
}

internal record GeminiSafetySetting
{
    [JsonPropertyName("category")]
    public required string Category { get; init; } // e.g., HARM_CATEGORY_SEXUALLY_EXPLICIT

    [JsonPropertyName("threshold")]
    public required string Threshold { get; init; } // e.g., BLOCK_MEDIUM_AND_ABOVE
}

internal record GeminiGenerateContentResponse
{
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate>? Candidates { get; init; }

    [JsonPropertyName("promptFeedback")]
    public GeminiPromptFeedback? PromptFeedback { get; init; }

    [JsonPropertyName("usageMetadata")]
    public GeminiUsageMetadata? UsageMetadata { get; init; } // Added UsageMetadata
}

internal record GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; init; }

    [JsonPropertyName("finishReason")]
    public string? FinishReason { get; init; } // e.g., STOP, MAX_TOKENS, SAFETY, RECITATION, OTHER

    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("safetyRatings")]
    public List<GeminiSafetyRating>? SafetyRatings { get; init; }

    // citationMetadata, tokenCount not directly mapped for now
}

internal record GeminiSafetyRating
{
    [JsonPropertyName("category")]
    public required string Category { get; init; }

    [JsonPropertyName("probability")]
    public required string Probability { get; init; } // e.g., NEGLIGIBLE, LOW, MEDIUM, HIGH

    [JsonPropertyName("blocked")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Blocked { get; init; }
}

internal record GeminiPromptFeedback
{
    [JsonPropertyName("blockReason")]
    public string? BlockReason { get; init; } // e.g., SAFETY, OTHER

    [JsonPropertyName("safetyRatings")]
    public List<GeminiSafetyRating>? SafetyRatings { get; init; }
}

// Added UsageMetadata based on documentation
internal record GeminiUsageMetadata
{
    [JsonPropertyName("promptTokenCount")]
    public int PromptTokenCount { get; init; }

    [JsonPropertyName("candidatesTokenCount")]
    public int CandidatesTokenCount { get; init; } // Sum of tokens across all candidates

    [JsonPropertyName("totalTokenCount")]
    public int TotalTokenCount { get; init; }
}

// Tool/Function calling support
internal record GeminiTool
{
    [JsonPropertyName("functionDeclarations")]
    public IEnumerable<GeminiFunctionDeclaration>? FunctionDeclarations { get; init; }
}

internal record GeminiFunctionDeclaration
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("parameters")]
    public object? Parameters { get; init; } // JSON Schema for the function parameters
}


// For Error Responses
// See: https://ai.google.dev/api/rest/v1beta/Code#error
internal record GeminiErrorResponse
{
    [JsonPropertyName("error")]
    public required GeminiErrorDetails Error { get; init; }
}

internal record GeminiErrorDetails
{
    [JsonPropertyName("code")]
    public int Code { get; init; } // e.g., 400

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; } // e.g., INVALID_ARGUMENT
}


// --- Internal Models for Model Listing ---
// See: https://ai.google.dev/api/rest/v1beta/models/list

internal record GeminiModelListResponse
{
    [JsonPropertyName("models")]
    public required List<GeminiModelData> Models { get; init; }

    // Optional: nextPageToken if using pagination
}

internal record GeminiModelData
{
    [JsonPropertyName("name")]
    public required string Name { get; init; } // Format: "models/{model_id}"

    [JsonPropertyName("version")]
    public string? Version { get; init; } // e.g., "1.0.0"

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; } // Human-readable name

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("inputTokenLimit")]
    public int? InputTokenLimit { get; init; }

    [JsonPropertyName("outputTokenLimit")]
    public int? OutputTokenLimit { get; init; }

    [JsonPropertyName("supportedGenerationMethods")]
    public List<string>? SupportedGenerationMethods { get; init; } // e.g., "generateContent", "streamGenerateContent"

    [JsonPropertyName("temperature")]
    public float? Temperature { get; init; } // Default temperature

    [JsonPropertyName("topP")]
    public float? TopP { get; init; } // Default topP

    [JsonPropertyName("topK")]
    public int? TopK { get; init; } // Default topK

    // Extract the model ID from the 'name' field
    [JsonIgnore]
    public string Id => Name.StartsWith("models/") ? Name.Substring("models/".Length) : Name;
}
