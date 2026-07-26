using System.Text.Json;
using System.Text.Json.Serialization;
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

/// <summary>Capabilities advertised for a model.</summary>
public sealed record ModelCapabilitiesDto(
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
    [property: JsonPropertyName("capabilities")] ModelCapabilitiesDto Capabilities,
    [property: JsonPropertyName("max_input_tokens")] int? MaxInputTokens,
    [property: JsonPropertyName("max_output_tokens")] int? MaxOutputTokens);

/// <summary>Model metadata response envelope.</summary>
public sealed record ModelMetadataResponse(string ModelId, ModelMetadataDto Metadata);

/// <summary>
/// Operator-configured pricing for a discovered model, in USD per million tokens.
/// Only the standard token rates and the pricing-model discriminator are projected;
/// clients should treat any <c>pricing_model</c> other than <c>standard</c> as too
/// complex to preview rather than rendering a number from these fields.
/// </summary>
public sealed record ModelPricingDto(
    [property: JsonPropertyName("pricing_model")] string PricingModel,
    [property: JsonPropertyName("input_cost_per_million_tokens")] decimal InputCostPerMillionTokens,
    [property: JsonPropertyName("output_cost_per_million_tokens")] decimal OutputCostPerMillionTokens,
    [property: JsonPropertyName("cached_input_cost_per_million_tokens")] decimal? CachedInputCostPerMillionTokens,
    [property: JsonPropertyName("embedding_cost_per_million_tokens")] decimal? EmbeddingCostPerMillionTokens,
    [property: JsonPropertyName("currency")] string Currency);

/// <summary>A model returned by the Conduit discovery extension.</summary>
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
    [property: JsonPropertyName("capabilities")] ModelCapabilitiesDto Capabilities,
    [property: JsonPropertyName("pricing")] ModelPricingDto? Pricing = null);

/// <summary>Model discovery response.</summary>
public sealed record DiscoveryModelsResponse(
    [property: JsonPropertyName("data")] IReadOnlyList<DiscoveredModelDto> Data,
    [property: JsonPropertyName("count")] int Count);

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

/// <summary>SignalR batching efficiency metrics.</summary>
public sealed record BatchingEfficiencyResponse(
    long TotalMessagesBatched,
    long TotalBatchesSent,
    double AverageMessagesPerBatch,
    long NetworkCallsSaved,
    double BatchEfficiencyPercentage,
    double AverageBatchLatency,
    bool IsBatchingEnabled);

/// <summary>Detailed active SignalR connections.</summary>
public sealed record ConnectionDetailsResponse(
    IReadOnlyList<ConduitLLM.Gateway.Models.ConnectionInfo> ActiveConnections,
    int Count);

/// <summary>A connection projected for hub diagnostics.</summary>
public sealed record HubConnectionDto(
    string ConnectionId,
    DateTime ConnectedAt,
    TimeSpan ConnectionDuration,
    IReadOnlyCollection<string> Groups,
    long MessagesSent,
    long MessagesAcknowledged);

/// <summary>Connections attached to a hub.</summary>
public sealed record HubConnectionsResponse(
    string HubName,
    IReadOnlyList<HubConnectionDto> Connections,
    int Count);

/// <summary>A connection projected for virtual-key diagnostics.</summary>
public sealed record VirtualKeyConnectionDto(
    string ConnectionId,
    string HubName,
    DateTime ConnectedAt,
    TimeSpan ConnectionDuration,
    IReadOnlyCollection<string> Groups);

/// <summary>Connections attached to a virtual key.</summary>
public sealed record VirtualKeyConnectionsResponse(
    int VirtualKeyId,
    IReadOnlyList<VirtualKeyConnectionDto> Connections,
    int Count);

/// <summary>A connection projected for group diagnostics.</summary>
public sealed record GroupConnectionDto(
    string ConnectionId,
    string HubName,
    DateTime ConnectedAt,
    int? VirtualKeyId);

/// <summary>Connections attached to a SignalR group.</summary>
public sealed record GroupConnectionsResponse(
    string GroupName,
    IReadOnlyList<GroupConnectionDto> Connections,
    int Count);

/// <summary>A dead-letter queue entry.</summary>
public sealed record DeadLetterMessageDto(
    string MessageId,
    string MessageType,
    string HubName,
    string MethodName,
    DateTime QueuedAt,
    int DeliveryAttempts,
    string? LastError,
    string? DeadLetterReason);

/// <summary>Dead-letter queue response.</summary>
public sealed record DeadLetterMessagesResponse(
    IReadOnlyList<DeadLetterMessageDto> Messages,
    int Count);

/// <summary>SignalR connection health summary.</summary>
public sealed record SignalRConnectionHealthDto(int Active, int Stale, string AcknowledgmentRate);

/// <summary>SignalR queue health summary.</summary>
public sealed record SignalRQueueHealthDto(
    int Pending,
    int DeadLetter,
    string CircuitBreaker,
    long Processed,
    long Failed);

/// <summary>Overall SignalR health response.</summary>
public sealed record SignalRHealthResponse(
    string Status,
    DateTime Timestamp,
    SignalRConnectionHealthDto Connections,
    SignalRQueueHealthDto Queue);

/// <summary>Public file metadata.</summary>
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
