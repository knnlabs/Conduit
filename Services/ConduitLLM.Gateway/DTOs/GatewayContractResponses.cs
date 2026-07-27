using System.Text.Json;
using System.Text.Json.Serialization;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Gateway.Models;

namespace ConduitLLM.Gateway.DTOs;

/// <summary>An OpenAI-compatible model entry.</summary>
public sealed record ModelListItemDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("object")] string Object,
    [property: JsonPropertyName("created")] long Created,
    [property: JsonPropertyName("owned_by")] string OwnedBy);

/// <summary>An OpenAI-compatible model list.</summary>
public sealed record ModelListResponse(
    [property: JsonPropertyName("data")] IReadOnlyList<ModelListItemDto> Data,
    [property: JsonPropertyName("object")] string Object);

/// <summary>Effective metadata for an enabled model mapping.</summary>
public sealed record ModelMetadataDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("canonical_model_id")] int CanonicalModelId,
    [property: JsonPropertyName("canonical_name")] string CanonicalName,
    [property: JsonPropertyName("provider")] string? Provider,
    [property: JsonPropertyName("provider_model_id")] string ProviderModelId,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("model_card_url")] string? ModelCardUrl,
    [property: JsonPropertyName("input_modalities")] IReadOnlyList<string> InputModalities,
    [property: JsonPropertyName("output_modalities")] IReadOnlyList<string> OutputModalities,
    [property: JsonPropertyName("capability_source")] string CapabilitySource,
    [property: JsonPropertyName("capabilities_last_verified_at")] DateTime? CapabilitiesLastVerifiedAt,
    [property: JsonPropertyName("capabilities")] DiscoveryModelCapabilitiesDto Capabilities,
    [property: JsonPropertyName("max_input_tokens")] int? MaxInputTokens,
    [property: JsonPropertyName("max_output_tokens")] int? MaxOutputTokens);

/// <summary>Model metadata response envelope.</summary>
public sealed record ModelMetadataResponse(string ModelId, ModelMetadataDto Metadata);

/// <summary>Known model capability names.</summary>
public sealed record DiscoveryCapabilitiesResponse(
    [property: JsonPropertyName("capabilities")] IReadOnlyList<string> Capabilities);

/// <summary>UI parameter metadata for a model.</summary>
public sealed record ModelParametersResponse(
    [property: JsonPropertyName("model_id")] int ModelId,
    [property: JsonPropertyName("model_alias")] string ModelAlias,
    [property: JsonPropertyName("series_name")] string SeriesName,
    [property: JsonPropertyName("parameters")] JsonElement Parameters);

/// <summary>Image task cancellation acknowledgement.</summary>
public sealed record TaskCancellationResponse(
    string Message,
    [property: JsonPropertyName("task_id")] string TaskId);

/// <summary>
/// Public file metadata contract. Kept separate from the Core storage model so
/// API requiredness and wire names remain stable as the internal model evolves.
/// </summary>
public sealed record FileMetadataResponse(
    [property: JsonPropertyName("file_name")] string? FileName,
    [property: JsonPropertyName("content_type")] string ContentType,
    [property: JsonPropertyName("size_bytes")] long SizeBytes,
    [property: JsonPropertyName("created_at")] DateTime? CreatedAt,
    [property: JsonPropertyName("modified_at")] DateTime? ModifiedAt,
    [property: JsonPropertyName("storage_provider")] string? StorageProvider,
    [property: JsonPropertyName("etag")] string? ETag,
    [property: JsonPropertyName("supports_range_requests")] bool SupportsRangeRequests,
    [property: JsonPropertyName("additional_metadata")] IReadOnlyDictionary<string, string>? AdditionalMetadata);

/// <summary>Temporary download URL response.</summary>
public sealed record DownloadUrlResponse(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("expires_at")] DateTime ExpiresAt,
    [property: JsonPropertyName("expiration_minutes")] int ExpirationMinutes);

/// <summary>Media upload response.</summary>
public sealed record MediaUploadResponse(
    bool Success,
    string StorageKey,
    string Url,
    string DirectUrl,
    string ContentType,
    string MediaType,
    string FileName,
    long SizeBytes);
