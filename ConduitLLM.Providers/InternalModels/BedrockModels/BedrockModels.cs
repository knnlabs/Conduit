using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ConduitLLM.Providers.InternalModels.BedrockModels;

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
