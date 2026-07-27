using System.Text.Json.Serialization;

namespace ConduitLLM.Configuration.DTOs;

/// <summary>Capabilities advertised for a discovered model.</summary>
public sealed record DiscoveryModelCapabilitiesDto(
    [property: JsonPropertyName("chat")] bool Chat,
    [property: JsonPropertyName("chat_stream")] bool ChatStream,
    [property: JsonPropertyName("image_input")] bool ImageInput,
    [property: JsonPropertyName("video_input")] bool VideoInput,
    [property: JsonPropertyName("audio_input")] bool AudioInput,
    [property: JsonPropertyName("file_input")] bool FileInput,
    [property: JsonPropertyName("vision")] bool Vision,
    [property: JsonPropertyName("video_understanding")] bool VideoUnderstanding,
    [property: JsonPropertyName("image_generation")] bool ImageGeneration,
    [property: JsonPropertyName("video_generation")] bool VideoGeneration,
    [property: JsonPropertyName("embeddings")] bool Embeddings,
    [property: JsonPropertyName("function_calling")] bool FunctionCalling,
    [property: JsonPropertyName("speech_to_text")] bool SpeechToText,
    [property: JsonPropertyName("text_to_speech")] bool TextToSpeech,
    [property: JsonPropertyName("rerank")] bool Rerank,
    [property: JsonPropertyName("tool_use")] bool? ToolUse = null,
    [property: JsonPropertyName("json_mode")] bool? JsonMode = null,
    [property: JsonPropertyName("max_tokens")] int? MaxTokens = null,
    [property: JsonPropertyName("max_output_tokens")] int? MaxOutputTokens = null,
    [property: JsonPropertyName("pdf_input")] bool PdfInput = false);

/// <summary>Operator-configured pricing for a discovered model.</summary>
public sealed record DiscoveryModelPricingDto(
    [property: JsonPropertyName("pricing_model")] string PricingModel,
    [property: JsonPropertyName("input_cost_per_million_tokens")] decimal InputCostPerMillionTokens,
    [property: JsonPropertyName("output_cost_per_million_tokens")] decimal OutputCostPerMillionTokens,
    [property: JsonPropertyName("cached_input_cost_per_million_tokens")] decimal? CachedInputCostPerMillionTokens,
    [property: JsonPropertyName("embedding_cost_per_million_tokens")] decimal? EmbeddingCostPerMillionTokens,
    [property: JsonPropertyName("currency")] string Currency);

/// <summary>A model returned by the Conduit discovery contract.</summary>
public sealed record DiscoveredModelDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("provider")] string? Provider,
    [property: JsonPropertyName("display_name")] string DisplayName,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("model_card_url")] string ModelCardUrl,
    [property: JsonPropertyName("max_tokens")] int MaxTokens,
    [property: JsonPropertyName("max_input_tokens")] int MaxInputTokens,
    [property: JsonPropertyName("max_output_tokens")] int MaxOutputTokens,
    [property: JsonPropertyName("tokenizer_type")] string TokenizerType,
    [property: JsonPropertyName("input_modalities")] IReadOnlyList<string> InputModalities,
    [property: JsonPropertyName("output_modalities")] IReadOnlyList<string> OutputModalities,
    [property: JsonPropertyName("capability_source")] string CapabilitySource,
    [property: JsonPropertyName("capabilities_last_verified_at")] DateTime? CapabilitiesLastVerifiedAt,
    [property: JsonPropertyName("parameters")] string Parameters,
    [property: JsonPropertyName("capabilities")] DiscoveryModelCapabilitiesDto Capabilities,
    [property: JsonPropertyName("pricing")] DiscoveryModelPricingDto? Pricing = null);

/// <summary>Model discovery response shared by Gateway discovery and Admin preview.</summary>
public sealed record DiscoveryModelsResponse(
    [property: JsonPropertyName("data")] IReadOnlyList<DiscoveredModelDto> Data,
    [property: JsonPropertyName("count")] int Count);
