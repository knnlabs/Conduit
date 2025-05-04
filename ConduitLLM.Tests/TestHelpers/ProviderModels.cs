namespace ConduitLLM.Tests.TestHelpers.Mocks;

/// <summary>
/// Mock implementation of OpenAIModelListResponse for testing
/// </summary>
public class OpenAIModelListResponse
{
    public string Object { get; set; } = "list";
    public List<OpenAIModelData> Data { get; set; } = new();
}

/// <summary>
/// Mock implementation of OpenAIModelInfo for testing
/// </summary>
public class OpenAIModelInfo
{
    public string Id { get; set; } = string.Empty;
    public string Object { get; set; } = "model";
    public long Created { get; set; }
    public string OwnedBy { get; set; } = string.Empty;
}

/// <summary>
/// Mock implementation of OpenAIChatCompletionChunk for testing
/// </summary>
public class OpenAIChatCompletionChunk
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("object")]
    public string Object { get; set; } = "chat.completion.chunk";

    [System.Text.Json.Serialization.JsonPropertyName("created")]
    public long Created { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("choices")]
    public List<OpenAIChunkChoice> Choices { get; set; } = new();
    
    [System.Text.Json.Serialization.JsonPropertyName("system_fingerprint")]
    public string? SystemFingerprint { get; set; }

    public static List<OpenAIChatCompletionChunk> GenerateChunks(int count)
    {
        var chunks = new List<OpenAIChatCompletionChunk>();
        for (int i = 0; i < count; i++)
        {
            chunks.Add(new OpenAIChatCompletionChunk
            {
                Id = $"chunk-{i}",
                Choices = new List<OpenAIChunkChoice>
                {
                    new OpenAIChunkChoice
                    {
                        Index = 0,
                        Delta = new OpenAIDelta { Role = "assistant", Content = $"content-{i}" },
                        FinishReason = i == count - 1 ? "stop" : null
                    }
                },
                Model = "gpt-4",
                SystemFingerprint = "fp_44455566" // Include a system fingerprint for testing
            });
        }
        return chunks;
    }
}

/// <summary>
/// Mock implementation of OpenAIChunkChoice for testing
/// </summary>
public class OpenAIChunkChoice
{
    [System.Text.Json.Serialization.JsonPropertyName("index")]
    public int Index { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("delta")]
    public OpenAIDelta Delta { get; set; } = new();

    [System.Text.Json.Serialization.JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

/// <summary>
/// Mock implementation of OpenAIDelta for testing
/// </summary>
public class OpenAIDelta
{
    [System.Text.Json.Serialization.JsonPropertyName("role")]
    public string? Role { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("content")]
    public string? Content { get; set; }
}

/// <summary>
/// Mock implementation of GeminiErrorDetail for testing
/// </summary>
public class GeminiErrorDetail
{
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Mock implementation of CohereResponseMetadata for testing
/// </summary>
public class CohereResponseMetadata
{
    public CohereTokenUsage? TokenUsage { get; set; }
    
    /// <summary>
    /// Token information
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("tokens")]
    public CohereApiMetaTokens? Tokens { get; set; }
    
    /// <summary>
    /// Billed units information
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("billed_units")]
    public CohereApiMetaBilledUnits? BilledUnits { get; set; }
}

/// <summary>
/// Mock implementation of CohereTokenUsage for testing
/// </summary>
public class CohereTokenUsage
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens => InputTokens + OutputTokens;
}

/// <summary>
/// Mock implementation of CohereApiMetaTokens for testing
/// </summary>
public class CohereApiMetaTokens
{
    [System.Text.Json.Serialization.JsonPropertyName("input_tokens")]
    public int? InputTokens { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("output_tokens")]
    public int? OutputTokens { get; set; }
}

/// <summary>
/// Mock implementation of CohereApiMetaBilledUnits for testing
/// </summary>
public class CohereApiMetaBilledUnits
{
    [System.Text.Json.Serialization.JsonPropertyName("input_tokens")]
    public int? InputTokens { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("output_tokens")]
    public int? OutputTokens { get; set; }
}

/// <summary>
/// Mock implementation of GeminiModelListResponse for testing
/// </summary>
public class GeminiModelListResponse
{
    public List<GeminiModelInfo> Models { get; set; } = new();
}

/// <summary>
/// Mock implementation of GeminiModelInfo for testing
/// </summary>
public class GeminiModelInfo
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long CreateTime { get; set; }
    public long UpdateTime { get; set; }
    public int InputTokenLimit { get; set; }
    public int OutputTokenLimit { get; set; }
    public List<string> SupportedGenerationMethods { get; set; } = new();
}

/// <summary>
/// Mock implementation of AnthropicMessageResponse for testing
/// </summary>
public class AnthropicMessageResponse
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "message";
    public string Model { get; set; } = string.Empty;
    public string Role { get; set; } = "assistant";
    public long Created { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public AnthropicContent Content { get; set; } = new();
    public string StopReason { get; set; } = "end_turn";
    public string? StopSequence { get; set; }
    public AnthropicUsage Usage { get; set; } = new();
}

/// <summary>
/// Mock implementation of AnthropicContent for testing
/// </summary>
public class AnthropicContent
{
    public List<AnthropicContentBlock> ContentBlocks { get; set; } = new();
}

/// <summary>
/// Mock implementation of AnthropicContentBlock for testing
/// </summary>
public class AnthropicContentBlock
{
    public string Type { get; set; } = "text";
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Mock implementation of AnthropicUsage for testing
/// </summary>
public class AnthropicUsage
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}

/// <summary>
/// Mock implementation of GeminiGenerateContentResponse for testing
/// </summary>
public class GeminiGenerateContentResponse
{
    public List<GeminiCandidate> Candidates { get; set; } = new();
    public GeminiUsage? Usage { get; set; }
    public GeminiPromptFeedback? PromptFeedback { get; set; }
}

/// <summary>
/// Mock implementation of GeminiCandidate for testing
/// </summary>
public class GeminiCandidate
{
    public GeminiContent Content { get; set; } = new();
    public string FinishReason { get; set; } = "STOP";
    public int Index { get; set; }
    public List<GeminiSafetyRating> SafetyRatings { get; set; } = new();
}

/// <summary>
/// Mock implementation of GeminiContent for testing
/// </summary>
public class GeminiContent
{
    public List<GeminiPart> Parts { get; set; } = new();
    public string Role { get; set; } = "model";
}

/// <summary>
/// Mock implementation of GeminiPart for testing
/// </summary>
public class GeminiPart
{
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Mock implementation of GeminiUsage for testing
/// </summary>
public class GeminiUsage
{
    public int PromptTokenCount { get; set; }
    public int CandidatesTokenCount { get; set; }
    public int TotalTokenCount => PromptTokenCount + CandidatesTokenCount;
}

/// <summary>
/// Mock implementation of CohereChatResponse for testing
/// </summary>
public class CohereChatResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("text")]
    public string Text { get; set; } = "Hello Cohere";

    [System.Text.Json.Serialization.JsonPropertyName("created")]
    public long Created { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    [System.Text.Json.Serialization.JsonPropertyName("generations")]
    public List<CohereMessage> Generations { get; set; } = new();

    [System.Text.Json.Serialization.JsonPropertyName("meta")]
    public CohereResponseMetadata? Meta { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("generation_id")]
    public string? GenerationId { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("chat_history")]
    public List<CohereMessage>? ChatHistory { get; set; }
}

/// <summary>
/// Mock implementation of CohereMessage for testing
/// </summary>
public class CohereMessage
{
    [System.Text.Json.Serialization.JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;
}

/// <summary>
/// Mock implementation of CohereStreamEventBase for testing
/// </summary>
public class CohereStreamEventBase
{
    [System.Text.Json.Serialization.JsonPropertyName("event_type")]
    public string EventType { get; set; } = string.Empty;
}

/// <summary>
/// Mock implementation of CohereStreamStartEvent for testing
/// </summary>
public class CohereStreamStartEvent : CohereStreamEventBase
{
    [System.Text.Json.Serialization.JsonPropertyName("generation_id")]
    public string GenerationId { get; set; } = string.Empty;
}

/// <summary>
/// Mock implementation of CohereTextGenerationEvent for testing
/// </summary>
public class CohereTextGenerationEvent : CohereStreamEventBase
{
    [System.Text.Json.Serialization.JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Mock implementation of CohereStreamEndEvent for testing
/// </summary>
public class CohereStreamEndEvent : CohereStreamEventBase
{
    [System.Text.Json.Serialization.JsonPropertyName("event_type")]
    public new string EventType { get; set; } = "stream-end";
    
    [System.Text.Json.Serialization.JsonPropertyName("is_finished")]
    public bool IsFinished { get; set; } = true;
    
    [System.Text.Json.Serialization.JsonPropertyName("finish_reason")]
    public string FinishReason { get; set; } = "COMPLETE";
    
    [System.Text.Json.Serialization.JsonPropertyName("response")]
    public CohereChatResponse Response { get; set; } = new();
}

/// <summary>
/// Mock implementation of AnthropicStreamErrorEvent for testing
/// </summary>
public class AnthropicStreamErrorEvent
{
    public string Type { get; set; } = "error";
    public AnthropicError Error { get; set; } = new();
}

/// <summary>
/// Mock implementation of AnthropicError for testing
/// </summary>
public class AnthropicError
{
    public string Type { get; set; } = "invalid_request_error";
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Mock implementation of AnthropicErrorResponse for testing
/// </summary>
public class AnthropicErrorResponse
{
    public string Type { get; set; } = "error";
    public AnthropicError Error { get; set; } = new();
}

/// <summary>
/// Mock implementation of AnthropicMessageStartEvent for testing
/// </summary>
public class AnthropicMessageStartEvent
{
    public string Type { get; set; } = "message_start";
    public AnthropicMessageResponse Message { get; set; } = new();
}

/// <summary>
/// Mock implementation of AnthropicContentBlockStartEvent for testing
/// </summary>
public class AnthropicContentBlockStartEvent
{
    public string Type { get; set; } = "content_block_start";
    public int Index { get; set; }
    public AnthropicContentBlock ContentBlock { get; set; } = new();
}

/// <summary>
/// Mock implementation of AnthropicPingEvent for testing
/// </summary>
public class AnthropicPingEvent
{
    public string Type { get; set; } = "ping";
}

/// <summary>
/// Mock implementation of AnthropicContentBlockDeltaEvent for testing
/// </summary>
public class AnthropicContentBlockDeltaEvent
{
    public string Type { get; set; } = "content_block_delta";
    public int Index { get; set; }
    public AnthropicStreamDelta Delta { get; set; } = new();
}

/// <summary>
/// Mock implementation of AnthropicStreamDelta for testing
/// </summary>
public class AnthropicStreamDelta
{
    public string Type { get; set; } = "text_delta";
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Mock implementation of AnthropicContentBlockStopEvent for testing
/// </summary>
public class AnthropicContentBlockStopEvent
{
    public string Type { get; set; } = "content_block_stop";
    public int Index { get; set; }
}

/// <summary>
/// Mock implementation of AnthropicMessageDeltaEvent for testing
/// </summary>
public class AnthropicMessageDeltaEvent
{
    public string Type { get; set; } = "message_delta";
    public AnthropicMessageDeltaDetails Delta { get; set; } = new();
    public AnthropicStreamUsage Usage { get; set; } = new();
}

/// <summary>
/// Mock implementation of AnthropicMessageDeltaDetails for testing
/// </summary>
public class AnthropicMessageDeltaDetails
{
    public string? StopReason { get; set; }
    public string? StopSequence { get; set; }
}

/// <summary>
/// Mock implementation of AnthropicStreamUsage for testing
/// </summary>
public class AnthropicStreamUsage
{
    public int OutputTokens { get; set; }
}

/// <summary>
/// Mock implementation of AnthropicMessageStopEvent for testing
/// </summary>
public class AnthropicMessageStopEvent
{
    public string Type { get; set; } = "message_stop";
}

/// <summary>
/// Mock implementation of AnthropicErrorDetails for testing
/// </summary>
public class AnthropicErrorDetails
{
    public string Type { get; set; } = "invalid_request_error";
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Mock implementation of GeminiPromptFeedback for testing
/// </summary>
public class GeminiPromptFeedback
{
    public string BlockReason { get; set; } = string.Empty;
    public string BlockReasonMessage { get; set; } = string.Empty;
}

/// <summary>
/// Mock implementation of GeminiSafetyRating for testing
/// </summary>
public class GeminiSafetyRating
{
    public string Category { get; set; } = string.Empty;
    public string Probability { get; set; } = string.Empty;
    public bool Blocked { get; set; }
}

/// <summary>
/// Mock implementation of GeminiListModelsResponse for testing
/// </summary>
public class GeminiListModelsResponse
{
    public List<GeminiModelInfo> Models { get; set; } = new();
}

/// <summary>
/// Mock implementation of GeminiErrorResponse for testing
/// </summary>
public class GeminiErrorResponse
{
    public GeminiErrorDetails Error { get; set; } = new();
}

/// <summary>
/// Mock implementation of GeminiErrorDetails for testing
/// </summary>
public class GeminiErrorDetails
{
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Status { get; set; }
}

/// <summary>
/// Mock implementation of GeminiGenerateContentRequest for testing
/// </summary>
public class GeminiGenerateContentRequest
{
    public List<GeminiContent> Contents { get; set; } = new();
    public GeminiGenerationConfig? GenerationConfig { get; set; }
}

/// <summary>
/// Mock implementation of GeminiGenerationConfig for testing
/// </summary>
public class GeminiGenerationConfig
{
    public float? Temperature { get; set; }
    public float? TopP { get; set; }
    public int? MaxOutputTokens { get; set; }
}

/// <summary>
/// Mock implementation of GeminiModelData for testing
/// </summary>
public class GeminiModelData
{
    public string Name { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public int InputTokenLimit { get; set; }
    public int OutputTokenLimit { get; set; }
    public List<string>? SupportedGenerationMethods { get; set; }
}

/// <summary>
/// Mock implementation of OpenAIModelData for testing
/// </summary>
public class OpenAIModelData
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("object")]
    public string Object { get; set; } = "model";

    [System.Text.Json.Serialization.JsonPropertyName("created")]
    public long Created { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("owned_by")]
    public string OwnedBy { get; set; } = string.Empty;
}

/// <summary>
/// Mock implementation of OpenAIChatCompletionResponse for testing
/// </summary>
public class OpenAIChatCompletionResponse
{
    public string Id { get; set; } = string.Empty;
    public string Object { get; set; } = "chat.completion";
    public long Created { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public string Model { get; set; } = string.Empty;
    public List<OpenAIChoice> Choices { get; set; } = new();
    public OpenAIUsage Usage { get; set; } = new();
    public string? SystemFingerprint { get; set; }
}

/// <summary>
/// Mock implementation of OpenAIChoice for testing
/// </summary>
public class OpenAIChoice
{
    public int Index { get; set; }
    public OpenAIMessage Message { get; set; } = new();
    public string FinishReason { get; set; } = "stop";
}

/// <summary>
/// Mock implementation of OpenAIMessage for testing
/// </summary>
public class OpenAIMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Mock implementation of OpenAIUsage for testing
/// </summary>
public class OpenAIUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}

/// <summary>
/// Mock implementation of OpenAIEmbeddingResponse for testing
/// </summary>
public class OpenAIEmbeddingResponse
{
    public string Object { get; set; } = "list";
    public List<OpenAIEmbedding> Data { get; set; } = new();
    public string Model { get; set; } = string.Empty;
    public OpenAIUsage Usage { get; set; } = new();
}

/// <summary>
/// Mock implementation of OpenAIEmbedding for testing
/// </summary>
public class OpenAIEmbedding
{
    public string Object { get; set; } = "embedding";
    public int Index { get; set; }
    public List<float> Embedding { get; set; } = new();
}

/// <summary>
/// Mock implementation of OpenAIChatCompletionRequest for testing
/// </summary>
public class OpenAIChatCompletionRequest
{
    public string Model { get; set; } = string.Empty;
    public List<OpenAIMessage> Messages { get; set; } = new();
    public float? Temperature { get; set; }
    public int? MaxTokens { get; set; }
    public bool Stream { get; set; }
}

/// <summary>
/// Mock implementation of OpenAIStreamingChoice for testing
/// </summary>
public class OpenAIStreamingChoice
{
    public int Index { get; set; }
    public OpenAIDeltaContent Delta { get; set; } = new();
    public string? FinishReason { get; set; }
}

/// <summary>
/// Mock implementation of OpenAIDeltaContent for testing
/// </summary>
public class OpenAIDeltaContent
{
    public string? Role { get; set; }
    public string? Content { get; set; }
}

/// <summary>
/// Mock implementation of AnthropicMessageRequest for testing
/// </summary>
public class AnthropicMessageRequest
{
    public string Model { get; set; } = string.Empty;
    public List<AnthropicMessage> Messages { get; set; } = new();
    public int MaxTokens { get; set; } = 1000;
    public string? SystemPrompt { get; set; }
    public float? Temperature { get; set; }
    public float? TopP { get; set; }
    public bool? Stream { get; set; }
}

/// <summary>
/// Mock implementation of AnthropicMessage for testing
/// </summary>
public class AnthropicMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Mock implementation of CohereChatRequest for testing
/// </summary>
public class CohereChatRequest
{
    public string Message { get; set; } = string.Empty;
    public string? Model { get; set; }
    public List<CohereMessage>? ChatHistory { get; set; }
    public string? Preamble { get; set; }
    public float? Temperature { get; set; }
    public int? MaxTokens { get; set; }
    public float? P { get; set; }
    public bool Stream { get; set; }
}

/// <summary>
/// Mock implementation of CohereErrorResponse for testing
/// </summary>
public class CohereErrorResponse
{
    public string? Message { get; set; }
}

/// <summary>
/// Mock implementation of MockChatCompletionRequest for testing
/// </summary>
public record MockChatCompletionRequest
{
    public string Model { get; set; } = string.Empty;
    public List<MockMessage> Messages { get; set; } = new();
    public int? MaxTokens { get; set; }
    public float? Temperature { get; set; }
    public float? TopP { get; set; }
    public bool Stream { get; set; }
}

/// <summary>
/// Mock implementation of MockMessage for testing
/// </summary>
public record MockMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
