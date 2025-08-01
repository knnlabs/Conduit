using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ConduitLLM.Providers.Providers.Bedrock.Models;

// AWS Bedrock Models
// Reference: https://docs.aws.amazon.com/bedrock/latest/userguide/model-parameters.html

/// <summary>
/// Base class for Bedrock requests
/// </summary>
public abstract class BedrockBaseRequest
{
    [JsonPropertyName("prompt")]
    public string? Prompt { get; set; }

    [JsonPropertyName("max_tokens_to_sample")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("temperature")]
    public float? Temperature { get; set; }

    [JsonPropertyName("top_p")]
    public float? TopP { get; set; }

    [JsonPropertyName("top_k")]
    public int? TopK { get; set; }

    [JsonPropertyName("stop_sequences")]
    public List<string>? StopSequences { get; set; }
}

/// <summary>
/// Anthropic Claude model request via Bedrock
/// </summary>
public class BedrockClaudeRequest : BedrockBaseRequest
{
    // Claude-specific parameters
}

/// <summary>
/// Amazon Titan model request via Bedrock
/// </summary>
public class BedrockTitanRequest : BedrockBaseRequest
{
    // Titan-specific parameters
}

/// <summary>
/// Bedrock Anthropic Claude chat request (for Claude 2/3)
/// </summary>
public class BedrockClaudeChatRequest
{
    [JsonPropertyName("anthropic_version")]
    public string AnthropicVersion { get; set; } = "bedrock-2023-05-31";

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("temperature")]
    public float? Temperature { get; set; }

    [JsonPropertyName("top_p")]
    public float? TopP { get; set; }

    [JsonPropertyName("top_k")]
    public int? TopK { get; set; }

    [JsonPropertyName("system")]
    public string? System { get; set; }

    [JsonPropertyName("messages")]
    public List<BedrockClaudeMessage> Messages { get; set; } = new();

    [JsonPropertyName("stop_sequences")]
    public List<string>? StopSequences { get; set; }

    [JsonPropertyName("stream")]
    public bool? Stream { get; set; }
}

/// <summary>
/// Represents a message in a Bedrock Claude chat request
/// </summary>
public class BedrockClaudeMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = null!;

    [JsonPropertyName("content")]
    public List<BedrockClaudeContent> Content { get; set; } = new();
}

/// <summary>
/// Message content that can be text or image
/// </summary>
public class BedrockClaudeContent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("source")]
    public ImageSource? Source { get; set; }
}

/// <summary>
/// Image source information for multimodal messages
/// </summary>
public class ImageSource
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "base64";

    [JsonPropertyName("media_type")]
    public string MediaType { get; set; } = "image/jpeg";

    [JsonPropertyName("data")]
    public string Data { get; set; } = null!;
}

/// <summary>
/// Bedrock Claude chat response
/// </summary>
public class BedrockClaudeChatResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public List<BedrockClaudeResponseContent>? Content { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }

    [JsonPropertyName("stop_sequence")]
    public string? StopSequence { get; set; }

    [JsonPropertyName("usage")]
    public BedrockClaudeUsage? Usage { get; set; }
}

/// <summary>
/// Bedrock Claude response content
/// </summary>
public class BedrockClaudeResponseContent
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

/// <summary>
/// Bedrock Claude usage metrics
/// </summary>
public class BedrockClaudeUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }
}

/// <summary>
/// Base response class for Bedrock models
/// </summary>
public class BedrockBaseResponse
{
    [JsonPropertyName("completion")]
    public string? Completion { get; set; }

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }
}

/// <summary>
/// Streaming response from Bedrock
/// </summary>
public class BedrockStreamingResponse
{
    [JsonPropertyName("completion")]
    public string? Completion { get; set; }

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }
}

/// <summary>
/// Bedrock embedding request
/// </summary>
public class BedrockEmbeddingRequest
{
    [JsonPropertyName("inputText")]
    public string InputText { get; set; } = null!;
}

/// <summary>
/// Bedrock embedding response
/// </summary>
public class BedrockEmbeddingResponse
{
    [JsonPropertyName("embedding")]
    public List<float> Embedding { get; set; } = new();
}

// --- Cohere Models for Bedrock ---

/// <summary>
/// Bedrock Cohere chat request
/// </summary>
public class BedrockCohereChatRequest
{
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = null!;
    
    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }
    
    [JsonPropertyName("temperature")]
    public float? Temperature { get; set; }
    
    [JsonPropertyName("p")]
    public float? P { get; set; } // Top-p
    
    [JsonPropertyName("k")]
    public int? K { get; set; } // Top-k
    
    [JsonPropertyName("stop_sequences")]
    public List<string>? StopSequences { get; set; }
    
    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;
}

/// <summary>
/// Bedrock Cohere chat response
/// </summary>
public class BedrockCohereChatResponse
{
    [JsonPropertyName("generations")]
    public List<BedrockCohereGeneration>? Generations { get; set; }
    
    [JsonPropertyName("generation_id")]
    public string? GenerationId { get; set; }
    
    [JsonPropertyName("text")]
    public string? Text { get; set; }
    
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
    
    [JsonPropertyName("meta")]
    public BedrockCohereMeta? Meta { get; set; }
}

public class BedrockCohereGeneration
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
    
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public class BedrockCohereMeta
{
    [JsonPropertyName("billed_units")]
    public BedrockCohereBilledUnits? BilledUnits { get; set; }
}

public class BedrockCohereBilledUnits
{
    [JsonPropertyName("input_tokens")]
    public int? InputTokens { get; set; }
    
    [JsonPropertyName("output_tokens")]
    public int? OutputTokens { get; set; }
}

/// <summary>
/// Bedrock Cohere embedding request
/// </summary>
public class BedrockCohereEmbeddingRequest
{
    [JsonPropertyName("texts")]
    public List<string> Texts { get; set; } = new();
    
    [JsonPropertyName("input_type")]
    public string? InputType { get; set; } = "search_document";
    
    [JsonPropertyName("truncate")]
    public string? Truncate { get; set; } = "END";
}

/// <summary>
/// Bedrock Cohere embedding response
/// </summary>
public class BedrockCohereEmbeddingResponse
{
    [JsonPropertyName("embeddings")]
    public List<List<float>> Embeddings { get; set; } = new();
    
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    
    [JsonPropertyName("response_type")]
    public string? ResponseType { get; set; }
    
    [JsonPropertyName("meta")]
    public BedrockCohereMeta? Meta { get; set; }
}

// --- Meta Llama Models for Bedrock ---

/// <summary>
/// Bedrock Meta Llama chat request
/// </summary>
public class BedrockLlamaChatRequest
{
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = null!;
    
    [JsonPropertyName("max_gen_len")]
    public int? MaxGenLen { get; set; }
    
    [JsonPropertyName("temperature")]
    public float? Temperature { get; set; }
    
    [JsonPropertyName("top_p")]
    public float? TopP { get; set; }
}

/// <summary>
/// Bedrock Meta Llama chat response
/// </summary>
public class BedrockLlamaChatResponse
{
    [JsonPropertyName("generation")]
    public string? Generation { get; set; }
    
    [JsonPropertyName("prompt_token_count")]
    public int? PromptTokenCount { get; set; }
    
    [JsonPropertyName("generation_token_count")]
    public int? GenerationTokenCount { get; set; }
    
    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }
}

// --- Amazon Titan Models for Bedrock ---

/// <summary>
/// Bedrock Amazon Titan chat request
/// </summary>
public class BedrockTitanChatRequest
{
    [JsonPropertyName("inputText")]
    public string InputText { get; set; } = null!;
    
    [JsonPropertyName("textGenerationConfig")]
    public BedrockTitanTextGenerationConfig? TextGenerationConfig { get; set; }
}

public class BedrockTitanTextGenerationConfig
{
    [JsonPropertyName("maxTokenCount")]
    public int? MaxTokenCount { get; set; }
    
    [JsonPropertyName("temperature")]
    public float? Temperature { get; set; }
    
    [JsonPropertyName("topP")]
    public float? TopP { get; set; }
    
    [JsonPropertyName("stopSequences")]
    public List<string>? StopSequences { get; set; }
}

/// <summary>
/// Bedrock Amazon Titan chat response
/// </summary>
public class BedrockTitanChatResponse
{
    [JsonPropertyName("inputTextTokenCount")]
    public int? InputTextTokenCount { get; set; }
    
    [JsonPropertyName("results")]
    public List<BedrockTitanResult>? Results { get; set; }
}

public class BedrockTitanResult
{
    [JsonPropertyName("tokenCount")]
    public int? TokenCount { get; set; }
    
    [JsonPropertyName("outputText")]
    public string? OutputText { get; set; }
    
    [JsonPropertyName("completionReason")]
    public string? CompletionReason { get; set; }
}

/// <summary>
/// Bedrock Amazon Titan embedding request
/// </summary>
public class BedrockTitanEmbeddingRequest
{
    [JsonPropertyName("inputText")]
    public string InputText { get; set; } = null!;
    
    [JsonPropertyName("dimensions")]
    public int? Dimensions { get; set; }
    
    [JsonPropertyName("normalize")]
    public bool? Normalize { get; set; }
}

/// <summary>
/// Bedrock Amazon Titan embedding response
/// </summary>
public class BedrockTitanEmbeddingResponse
{
    [JsonPropertyName("embedding")]
    public List<float> Embedding { get; set; } = new();
    
    [JsonPropertyName("inputTextTokenCount")]
    public int? InputTextTokenCount { get; set; }
}

// --- AI21 Models for Bedrock ---

/// <summary>
/// Bedrock AI21 chat request
/// </summary>
public class BedrockAI21ChatRequest
{
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = null!;
    
    [JsonPropertyName("maxTokens")]
    public int? MaxTokens { get; set; }
    
    [JsonPropertyName("temperature")]
    public float? Temperature { get; set; }
    
    [JsonPropertyName("topP")]
    public float? TopP { get; set; }
    
    [JsonPropertyName("stopSequences")]
    public List<string>? StopSequences { get; set; }
    
    [JsonPropertyName("countPenalty")]
    public BedrockAI21Penalty? CountPenalty { get; set; }
    
    [JsonPropertyName("presencePenalty")]
    public BedrockAI21Penalty? PresencePenalty { get; set; }
}

public class BedrockAI21Penalty
{
    [JsonPropertyName("scale")]
    public float Scale { get; set; }
}

/// <summary>
/// Bedrock AI21 chat response
/// </summary>
public class BedrockAI21ChatResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    
    [JsonPropertyName("completions")]
    public List<BedrockAI21Completion>? Completions { get; set; }
}

public class BedrockAI21Completion
{
    [JsonPropertyName("data")]
    public BedrockAI21CompletionData? Data { get; set; }
    
    [JsonPropertyName("finishReason")]
    public BedrockAI21FinishReason? FinishReason { get; set; }
}

public class BedrockAI21CompletionData
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
    
    [JsonPropertyName("tokens")]
    public List<BedrockAI21Token>? Tokens { get; set; }
}

public class BedrockAI21Token
{
    [JsonPropertyName("generatedToken")]
    public BedrockAI21GeneratedToken? GeneratedToken { get; set; }
}

public class BedrockAI21GeneratedToken
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }
}

public class BedrockAI21FinishReason
{
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

// --- Mistral Models for Bedrock ---

/// <summary>
/// Bedrock Mistral chat request
/// </summary>
public class BedrockMistralChatRequest
{
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = null!;
    
    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }
    
    [JsonPropertyName("temperature")]
    public float? Temperature { get; set; }
    
    [JsonPropertyName("top_p")]
    public float? TopP { get; set; }
    
    [JsonPropertyName("top_k")]
    public int? TopK { get; set; }
    
    [JsonPropertyName("stop")]
    public List<string>? Stop { get; set; }
}

/// <summary>
/// Bedrock Mistral chat response
/// </summary>
public class BedrockMistralChatResponse
{
    [JsonPropertyName("outputs")]
    public List<BedrockMistralOutput>? Outputs { get; set; }
}

public class BedrockMistralOutput
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
    
    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }
}

/// <summary>
/// Bedrock Mistral streaming response
/// </summary>
public class BedrockMistralStreamingResponse
{
    [JsonPropertyName("outputs")]
    public List<BedrockMistralStreamingOutput>? Outputs { get; set; }
}

public class BedrockMistralStreamingOutput
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
    
    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }
}

// --- Stability AI Models for Bedrock ---

/// <summary>
/// Bedrock Stability AI image generation request
/// </summary>
public class BedrockStabilityImageRequest
{
    [JsonPropertyName("text_prompts")]
    public List<BedrockStabilityTextPrompt> TextPrompts { get; set; } = new();
    
    [JsonPropertyName("cfg_scale")]
    public int? CfgScale { get; set; } = 7;
    
    [JsonPropertyName("clip_guidance_preset")]
    public string? ClipGuidancePreset { get; set; } = "NONE";
    
    [JsonPropertyName("height")]
    public int? Height { get; set; } = 512;
    
    [JsonPropertyName("width")]
    public int? Width { get; set; } = 512;
    
    [JsonPropertyName("samples")]
    public int? Samples { get; set; } = 1;
    
    [JsonPropertyName("seed")]
    public int? Seed { get; set; }
    
    [JsonPropertyName("steps")]
    public int? Steps { get; set; } = 50;
    
    [JsonPropertyName("style_preset")]
    public string? StylePreset { get; set; }
    
    [JsonPropertyName("sampler")]
    public string? Sampler { get; set; }
}

/// <summary>
/// Text prompt for Stability AI
/// </summary>
public class BedrockStabilityTextPrompt
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = null!;
    
    [JsonPropertyName("weight")]
    public float? Weight { get; set; } = 1.0f;
}

/// <summary>
/// Bedrock Stability AI image generation response
/// </summary>
public class BedrockStabilityImageResponse
{
    [JsonPropertyName("result")]
    public string? Result { get; set; }
    
    [JsonPropertyName("artifacts")]
    public List<BedrockStabilityArtifact>? Artifacts { get; set; }
}

/// <summary>
/// Stability AI image artifact
/// </summary>
public class BedrockStabilityArtifact
{
    [JsonPropertyName("base64")]
    public string? Base64 { get; set; }
    
    [JsonPropertyName("seed")]
    public int? Seed { get; set; }
    
    [JsonPropertyName("finishReason")]
    public string? FinishReason { get; set; }
}

// --- Streaming Response Models ---

/// <summary>
/// Bedrock Claude streaming response
/// </summary>
public class BedrockClaudeStreamingResponse
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    
    [JsonPropertyName("index")]
    public int? Index { get; set; }
    
    [JsonPropertyName("delta")]
    public BedrockClaudeStreamingDelta? Delta { get; set; }
    
    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }
    
    [JsonPropertyName("stop_sequence")]
    public string? StopSequence { get; set; }
    
    [JsonPropertyName("usage")]
    public BedrockClaudeUsage? Usage { get; set; }
}

/// <summary>
/// Claude streaming delta content
/// </summary>
public class BedrockClaudeStreamingDelta
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

/// <summary>
/// Bedrock Cohere streaming response
/// </summary>
public class BedrockCohereStreamingResponse
{
    [JsonPropertyName("is_finished")]
    public bool? IsFinished { get; set; }
    
    [JsonPropertyName("event_type")]
    public string? EventType { get; set; }
    
    [JsonPropertyName("text")]
    public string? Text { get; set; }
    
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
    
    [JsonPropertyName("generation_id")]
    public string? GenerationId { get; set; }
}

/// <summary>
/// Bedrock Llama streaming response
/// </summary>
public class BedrockLlamaStreamingResponse
{
    [JsonPropertyName("generation")]
    public string? Generation { get; set; }
    
    [JsonPropertyName("prompt_token_count")]
    public int? PromptTokenCount { get; set; }
    
    [JsonPropertyName("generation_token_count")]
    public int? GenerationTokenCount { get; set; }
    
    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }
}
