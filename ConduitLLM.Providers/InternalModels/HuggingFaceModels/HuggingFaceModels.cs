using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ConduitLLM.Providers.InternalModels.HuggingFaceModels;

/// <summary>
/// HuggingFace Inference API text generation request
/// </summary>
public class HuggingFaceTextGenerationRequest
{
    [JsonPropertyName("inputs")]
    public string Inputs { get; set; } = null!;

    [JsonPropertyName("parameters")]
    public HuggingFaceParameters? Parameters { get; set; }

    [JsonPropertyName("options")]
    public HuggingFaceOptions? Options { get; set; }
}

/// <summary>
/// HuggingFace Inference API parameters
/// </summary>
public class HuggingFaceParameters
{
    [JsonPropertyName("max_new_tokens")]
    public int? MaxNewTokens { get; set; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }

    [JsonPropertyName("top_k")]
    public int? TopK { get; set; }

    [JsonPropertyName("repetition_penalty")]
    public double? RepetitionPenalty { get; set; }

    [JsonPropertyName("do_sample")]
    public bool? DoSample { get; set; }

    [JsonPropertyName("stop")]
    public List<string>? Stop { get; set; }

    [JsonPropertyName("return_full_text")]
    public bool? ReturnFullText { get; set; }
}

/// <summary>
/// HuggingFace Inference API options
/// </summary>
public class HuggingFaceOptions
{
    [JsonPropertyName("use_cache")]
    public bool? UseCache { get; set; }

    [JsonPropertyName("wait_for_model")]
    public bool? WaitForModel { get; set; }
}

/// <summary>
/// HuggingFace Inference API text generation response
/// </summary>
public class HuggingFaceTextGenerationResponse
{
    [JsonPropertyName("generated_text")]
    public string? GeneratedText { get; set; }
}

/// <summary>
/// HuggingFace Inference API chat request
/// </summary>
public class HuggingFaceChatRequest
{
    [JsonPropertyName("inputs")]
    public HuggingFaceChatInputs Inputs { get; set; } = new();

    [JsonPropertyName("parameters")]
    public HuggingFaceParameters? Parameters { get; set; }

    [JsonPropertyName("options")]
    public HuggingFaceOptions? Options { get; set; }
}

/// <summary>
/// HuggingFace chat inputs
/// </summary>
public class HuggingFaceChatInputs
{
    [JsonPropertyName("past_user_inputs")]
    public List<string>? PastUserInputs { get; set; }

    [JsonPropertyName("generated_responses")]
    public List<string>? GeneratedResponses { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

/// <summary>
/// HuggingFace chat response
/// </summary>
public class HuggingFaceChatResponse
{
    [JsonPropertyName("generated_text")]
    public string? GeneratedText { get; set; }

    [JsonPropertyName("conversation")]
    public HuggingFaceConversation? Conversation { get; set; }
}

/// <summary>
/// HuggingFace conversation
/// </summary>
public class HuggingFaceConversation
{
    [JsonPropertyName("past_user_inputs")]
    public List<string>? PastUserInputs { get; set; }

    [JsonPropertyName("generated_responses")]
    public List<string>? GeneratedResponses { get; set; }
}

/// <summary>
/// HuggingFace embedding request
/// </summary>
public class HuggingFaceEmbeddingRequest
{
    [JsonPropertyName("inputs")]
    public string[] Inputs { get; set; } = null!;

    [JsonPropertyName("options")]
    public HuggingFaceOptions? Options { get; set; }
}

/// <summary>
/// HuggingFace feature extraction response (embeddings)
/// </summary>
public class HuggingFaceEmbeddingResponse
{
    // Can be either List<List<float>> for batched requests or List<float> for single requests
    [JsonPropertyName("features")]
    public List<List<float>>? Features { get; set; }
}

/// <summary>
/// HuggingFace image generation request
/// </summary>
public class HuggingFaceImageGenerationRequest
{
    [JsonPropertyName("inputs")]
    public string Inputs { get; set; } = null!;

    [JsonPropertyName("parameters")]
    public HuggingFaceImageParameters? Parameters { get; set; }

    [JsonPropertyName("options")]
    public HuggingFaceOptions? Options { get; set; }
}

/// <summary>
/// HuggingFace image generation parameters
/// </summary>
public class HuggingFaceImageParameters
{
    [JsonPropertyName("guidance_scale")]
    public double? GuidanceScale { get; set; }

    [JsonPropertyName("negative_prompt")]
    public string? NegativePrompt { get; set; }

    [JsonPropertyName("num_inference_steps")]
    public int? NumInferenceSteps { get; set; }

    [JsonPropertyName("seed")]
    public int? Seed { get; set; }

    [JsonPropertyName("target_size")]
    public HuggingFaceImageSize? TargetSize { get; set; }
}

/// <summary>
/// HuggingFace image size
/// </summary>
public class HuggingFaceImageSize
{
    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}

/// <summary>
/// HuggingFace image generation response (binary data)
/// </summary>
public class HuggingFaceImageGenerationResponse
{
    // HuggingFace returns raw binary image data (PNG/JPEG)
    // We'll handle this differently in the client
    public byte[] ImageData { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "image/png";
}

/// <summary>
/// HuggingFace pipeline request for newer Inference API endpoints
/// </summary>
public class HuggingFacePipelineRequest
{
    [JsonPropertyName("inputs")]
    public object Inputs { get; set; } = null!;

    [JsonPropertyName("parameters")]
    public object? Parameters { get; set; }
}
