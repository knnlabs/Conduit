using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ConduitLLM.Providers.Providers.SageMaker.Models.SageMakerModels;

/// <summary>
/// SageMaker base request class for text generation
/// </summary>
public class SageMakerRequest
{
    [JsonPropertyName("inputs")]
    public string Inputs { get; set; } = null!;

    [JsonPropertyName("parameters")]
    public SageMakerParameters? Parameters { get; set; }
}

/// <summary>
/// SageMaker parameters for generation
/// </summary>
public class SageMakerParameters
{
    [JsonPropertyName("max_new_tokens")]
    public int? MaxNewTokens { get; set; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }

    [JsonPropertyName("top_k")]
    public int? TopK { get; set; }

    [JsonPropertyName("stop")]
    public List<string>? Stop { get; set; }

    [JsonPropertyName("do_sample")]
    public bool? DoSample { get; set; }

    [JsonPropertyName("return_full_text")]
    public bool? ReturnFullText { get; set; }
}

/// <summary>
/// SageMaker text generation response
/// </summary>
public class SageMakerResponse
{
    [JsonPropertyName("generated_text")]
    public string? GeneratedText { get; set; }
}

/// <summary>
/// SageMaker chat request format
/// </summary>
public class SageMakerChatRequest
{
    [JsonPropertyName("inputs")]
    public List<SageMakerChatMessage> Inputs { get; set; } = new();

    [JsonPropertyName("parameters")]
    public SageMakerParameters? Parameters { get; set; }
}

/// <summary>
/// SageMaker chat message
/// </summary>
public class SageMakerChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = null!;

    [JsonPropertyName("content")]
    public string Content { get; set; } = null!;
}

/// <summary>
/// SageMaker chat response
/// </summary>
public class SageMakerChatResponse
{
    [JsonPropertyName("generated_outputs")]
    public List<SageMakerChatOutput>? GeneratedOutputs { get; set; }
}

/// <summary>
/// SageMaker chat output
/// </summary>
public class SageMakerChatOutput
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

/// <summary>
/// SageMaker embedding request
/// </summary>
public class SageMakerEmbeddingRequest
{
    [JsonPropertyName("inputs")]
    public List<string> Inputs { get; set; } = new();
}

/// <summary>
/// SageMaker embedding response
/// </summary>
public class SageMakerEmbeddingResponse
{
    [JsonPropertyName("embeddings")]
    public List<List<float>> Embeddings { get; set; } = new();
}
