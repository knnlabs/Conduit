using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Admin.DTOs;

public sealed record MetricKeyCountDto(string Key, long Count);

public sealed record AnalyticsCacheMetricsResponse(
    long TotalHits,
    long TotalMisses,
    double HitRate,
    long TotalInvalidations,
    double UptimeMinutes,
    IReadOnlyList<MetricKeyCountDto> TopHitKeys,
    IReadOnlyList<MetricKeyCountDto> TopMissKeys);

public sealed record AnalyticsCacheInvalidationResponse(
    string Message,
    string Reason,
    int KeysInvalidated);

public sealed record BillingRevenueLossResponse(decimal PotentialRevenueLoss, string Currency);

public sealed record BillingAuditEventTypeResponse(
    BillingAuditEventType Value,
    string Name,
    string Description);

public sealed record BatchSpendingFlushResponse(
    bool Success,
    string Message,
    string RequestId,
    DateTime RequestedAt,
    string Priority,
    string EstimatedProcessingTime,
    string Note);

public sealed record BatchSpendingArchitectureResponse(
    string Pattern,
    string AdminRole,
    string CoreRole,
    string Decoupling);

public sealed record BatchSpendingStatusResponse(
    bool Success,
    string AdminApiStatus,
    bool EventBusAvailable,
    bool CanPublishFlushRequests,
    IReadOnlyList<string> SupportedOperations,
    BatchSpendingArchitectureResponse Architecture,
    DateTime Timestamp);

public sealed record BatchSpendingParameterInfo(
    string Reason,
    string Priority,
    string TimeoutSeconds,
    string IncludeStatistics);

public sealed record BatchSpendingEndpointInfo(
    string Method,
    string Path,
    string Description,
    BatchSpendingParameterInfo? Parameters = null,
    IReadOnlyList<string>? UseCases = null);

public sealed record BatchSpendingEndpointsInfo(
    BatchSpendingEndpointInfo Flush,
    BatchSpendingEndpointInfo Status);

public sealed record BatchSpendingInformationArchitecture(
    string Pattern,
    string Security,
    string Reliability,
    string Monitoring);

public sealed record BatchSpendingInformationResponse(
    string Service,
    string Description,
    BatchSpendingEndpointsInfo Endpoints,
    BatchSpendingInformationArchitecture Architecture,
    IReadOnlyList<string> OperationalNotes,
    DateTime Timestamp);

public sealed record CacheInvalidationPublishedResponse(
    string Message,
    DateTime Timestamp,
    string Note);

public sealed record CacheServiceUnavailableResponse(string Message, string Note);

public sealed record GlobalSettingsReloadAcceptedResponse(
    string Message,
    string RequestId,
    DateTime AcceptedAt);

public sealed record MediaRecordResponse(
    Guid Id,
    string StorageKey,
    int VirtualKeyId,
    string MediaType,
    string? ContentType,
    long? SizeBytes,
    string? ContentHash,
    string? Provider,
    string? Model,
    string? Prompt,
    string? StorageUrl,
    string? PublicUrl,
    DateTime? ExpiresAt,
    DateTime CreatedAt,
    DateTime? LastAccessedAt,
    int AccessCount,
    DateTime? DeletedAt);
