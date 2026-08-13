using System.Text.Json;
using System.Text.Json.Serialization;

using ConduitLLM.Providers.Bedrock;
using ConduitLLM.Providers.OpenRouter;

namespace ConduitLLM.Providers.Serialization;

/// <summary>
/// Source-generated metadata for selected high-volume provider request/response paths.
/// </summary>
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BedrockConverseRequest))]
[JsonSerializable(typeof(BedrockConverseResponse))]
[JsonSerializable(typeof(BedrockRawToolArguments))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(List<JsonElement>))]
[JsonSerializable(typeof(BedrockListFoundationModelsResponse))]
[JsonSerializable(typeof(BedrockStreamMessageStart))]
[JsonSerializable(typeof(BedrockStreamContentBlockStart))]
[JsonSerializable(typeof(BedrockStreamContentBlockDelta))]
[JsonSerializable(typeof(BedrockStreamMessageStop))]
[JsonSerializable(typeof(BedrockStreamMetadata))]
[JsonSerializable(typeof(OpenRouterCatalogResponse))]
internal partial class ProvidersJsonContext : JsonSerializerContext;
