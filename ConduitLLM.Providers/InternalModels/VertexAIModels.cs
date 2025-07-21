using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ConduitLLM.Providers.InternalModels;

/// <summary>
/// Vertex AI prediction request base class
/// </summary>
public class VertexAIPredictionRequest
{
    [JsonPropertyName("instances")]
    public List<object> Instances { get; set; } = new();

    [JsonPropertyName("parameters")]
    public VertexAIParameters? Parameters { get; set; }
}

/// <summary>
/// Parameters for Vertex AI prediction
/// </summary>
public class VertexAIParameters
{
    [JsonPropertyName("temperature")]
    public float? Temperature { get; set; }

    [JsonPropertyName("maxOutputTokens")]
    public int? MaxOutputTokens { get; set; }

    [JsonPropertyName("topP")]
    public float? TopP { get; set; }

    [JsonPropertyName("topK")]
    public int? TopK { get; set; }
}

/// <summary>
/// Vertex AI PaLM chat request
/// </summary>
public class VertexAIPaLMInstance
{
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = null!;
}

/// <summary>
/// Vertex AI Gemini chat request
/// </summary>
public class VertexAIGeminiRequest
{
    [JsonPropertyName("contents")]
    public List<VertexAIGeminiContent> Contents { get; set; } = new();

    [JsonPropertyName("generationConfig")]
    public VertexAIGenerationConfig? GenerationConfig { get; set; }

    [JsonPropertyName("systemInstruction")]
    public VertexAIGeminiContent? SystemInstruction { get; set; }
}

/// <summary>
/// Generation configuration for Vertex AI Gemini models
/// </summary>
public class VertexAIGenerationConfig
{
    [JsonPropertyName("temperature")]
    public float? Temperature { get; set; }

    [JsonPropertyName("maxOutputTokens")]
    public int? MaxOutputTokens { get; set; }

    [JsonPropertyName("topP")]
    public float? TopP { get; set; }

    [JsonPropertyName("topK")]
    public int? TopK { get; set; }
}

/// <summary>
/// Vertex AI Gemini content
/// </summary>
public class VertexAIGeminiContent
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("parts")]
    public List<VertexAIGeminiPart> Parts { get; set; } = new();
}

/// <summary>
/// Vertex AI Gemini content part
/// </summary>
public class VertexAIGeminiPart
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("inlineData")]
    public VertexAIInlineData? InlineData { get; set; }
}

/// <summary>
/// Vertex AI inline data for multimodal content
/// </summary>
public class VertexAIInlineData
{
    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = "image/jpeg";

    [JsonPropertyName("data")]
    public string Data { get; set; } = null!;
}

/// <summary>
/// Vertex AI prediction response
/// </summary>
public class VertexAIPredictionResponse
{
    [JsonPropertyName("predictions")]
    public List<VertexAIPrediction>? Predictions { get; set; }
}

/// <summary>
/// Vertex AI prediction result
/// </summary>
public class VertexAIPrediction
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    // For structured models like Gemini
    [JsonPropertyName("candidates")]
    public List<VertexAIGeminiCandidate>? Candidates { get; set; }

    [JsonPropertyName("safetyAttributes")]
    public VertexAISafetyAttributes? SafetyAttributes { get; set; }
}

/// <summary>
/// Vertex AI Gemini candidate response
/// </summary>
public class VertexAIGeminiCandidate
{
    [JsonPropertyName("content")]
    public VertexAIGeminiContent? Content { get; set; }

    [JsonPropertyName("finishReason")]
    public string? FinishReason { get; set; }
}

/// <summary>
/// Vertex AI safety attributes
/// </summary>
public class VertexAISafetyAttributes
{
    [JsonPropertyName("blocked")]
    public bool Blocked { get; set; }

    [JsonPropertyName("scores")]
    public List<float>? Scores { get; set; }

    [JsonPropertyName("categories")]
    public List<string>? Categories { get; set; }
}

/// <summary>
/// Vertex AI embedding request
/// </summary>
public class VertexAIEmbeddingRequest
{
    [JsonPropertyName("instances")]
    public List<VertexAIEmbeddingInstance> Instances { get; set; } = new();
}

/// <summary>
/// Vertex AI embedding instance
/// </summary>
public class VertexAIEmbeddingInstance
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = null!;
}

/// <summary>
/// Vertex AI embedding response
/// </summary>
public class VertexAIEmbeddingResponse
{
    [JsonPropertyName("predictions")]
    public List<VertexAIEmbeddingPrediction>? Predictions { get; set; }
}

/// <summary>
/// Vertex AI embedding prediction
/// </summary>
public class VertexAIEmbeddingPrediction
{
    [JsonPropertyName("embeddings")]
    public VertexAIEmbedding? Embeddings { get; set; }
}

/// <summary>
/// Vertex AI embedding result
/// </summary>
public class VertexAIEmbedding
{
    [JsonPropertyName("values")]
    public List<float>? Values { get; set; }
}
