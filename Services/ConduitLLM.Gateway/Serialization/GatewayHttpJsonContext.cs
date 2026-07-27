using System.Text.Json.Serialization;

using ConduitLLM.Gateway.DTOs;

namespace ConduitLLM.Gateway.Serialization;

/// <summary>
/// Source-generated metadata for Gateway-owned HTTP response contracts.
/// </summary>
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ModelListResponse))]
[JsonSerializable(typeof(ModelMetadataResponse))]
[JsonSerializable(typeof(DiscoveryCapabilitiesResponse))]
[JsonSerializable(typeof(ModelParametersResponse))]
[JsonSerializable(typeof(TaskCancellationResponse))]
[JsonSerializable(typeof(FileMetadataResponse))]
[JsonSerializable(typeof(DownloadUrlResponse))]
[JsonSerializable(typeof(MediaUploadResponse))]
public partial class GatewayHttpJsonContext : JsonSerializerContext;
