using System.Text.Json;
using System.Text.Json.Serialization;

using ConduitLLM.Providers.Bedrock;
using ConduitLLM.Providers.OpenRouter;
using ConduitLLM.Providers.Cloudflare;
using ConduitLLM.Providers.Replicate;

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
[JsonSerializable(typeof(CloudflareImageResponse))]
[JsonSerializable(typeof(ReplicatePredictionRequest))]
[JsonSerializable(typeof(ReplicatePredictionResponse))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(decimal))]
internal partial class ProvidersJsonContext : JsonSerializerContext;
