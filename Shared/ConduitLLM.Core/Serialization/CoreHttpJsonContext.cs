using System.Text.Json;
using System.Text.Json.Serialization;

using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Serialization;

/// <summary>
/// Source-generated metadata for the highest-volume OpenAI-compatible HTTP contracts.
/// Metadata mode deliberately keeps the caller's naming, converter, and ignore policies
/// authoritative so Gateway wire behavior is unchanged.
/// </summary>
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ChatCompletionRequest))]
[JsonSerializable(typeof(ChatCompletionResponse))]
[JsonSerializable(typeof(ChatCompletionChunk))]
[JsonSerializable(typeof(global::ConduitLLM.Core.Models.Audio.TextToSpeechRequest))]
[JsonSerializable(typeof(global::ConduitLLM.Core.Models.Audio.AudioTranscriptionResponse))]
[JsonSerializable(typeof(EmbeddingRequest))]
[JsonSerializable(typeof(ImageGenerationRequest))]
[JsonSerializable(typeof(global::ConduitLLM.Core.Models.Rerank.RerankRequest))]
[JsonSerializable(typeof(global::ConduitLLM.Core.Models.Responses.CreateResponseRequest))]
[JsonSerializable(typeof(VideoGenerationRequest))]
[JsonSerializable(typeof(OpenAIErrorResponse))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(List<object>))]
[JsonSerializable(typeof(object[]))]
public partial class CoreHttpJsonContext : JsonSerializerContext;
