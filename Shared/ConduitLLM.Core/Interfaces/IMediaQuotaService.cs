using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Core.Interfaces;

/// <summary>
/// Evaluates and reports virtual-key-group media storage quotas.
/// </summary>
public interface IMediaQuotaService
{
    /// <summary>
    /// Rejects a prospective write when the owning group's policy requires it.
    /// </summary>
    Task EnsureCanStoreAsync(
        int virtualKeyId,
        long prospectiveSizeBytes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets quota usage for one group or all groups using aggregate database queries.
    /// </summary>
    Task<IReadOnlyList<MediaGroupQuotaUsage>> GetGroupUsagesAsync(
        int? virtualKeyGroupId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Singleton-safe entry point used by storage backends before uploading an object.
/// </summary>
public interface IMediaQuotaGuard
{
    Task EnsureCanStoreAsync(
        string? createdBy,
        long prospectiveSizeBytes,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Current usage and effective quota for a virtual-key group.
/// </summary>
public sealed class MediaGroupQuotaUsage
{
    public int VirtualKeyGroupId { get; set; }
    public string VirtualKeyGroupName { get; set; } = string.Empty;
    public int? MediaRetentionPolicyId { get; set; }
    public string? MediaRetentionPolicyName { get; set; }
    public long TotalSizeBytes { get; set; }
    public int TotalFiles { get; set; }
    public long? MaxStorageSizeBytes { get; set; }
    public int? MaxFileCount { get; set; }
    public MediaQuotaExceededBehavior QuotaExceededBehavior { get; set; }
    public bool RespectRecentAccess { get; set; }
    public int RecentAccessWindowDays { get; set; }

    public bool IsOverQuota =>
        (MaxStorageSizeBytes.HasValue && TotalSizeBytes > MaxStorageSizeBytes.Value) ||
        (MaxFileCount.HasValue && TotalFiles > MaxFileCount.Value);

    public double? StorageUsagePercent =>
        MaxStorageSizeBytes is > 0
            ? (double)TotalSizeBytes / MaxStorageSizeBytes.Value * 100
            : null;

    public double? FileUsagePercent =>
        MaxFileCount is > 0
            ? (double)TotalFiles / MaxFileCount.Value * 100
            : null;
}
