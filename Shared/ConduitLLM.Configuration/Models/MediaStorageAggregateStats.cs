namespace ConduitLLM.Configuration.Models;

/// <summary>
/// Database-computed media storage statistics.
/// </summary>
public sealed class MediaStorageAggregateStats
{
    public int TotalFiles { get; init; }
    public long TotalSizeBytes { get; init; }
    public IReadOnlyDictionary<string, long> ByProvider { get; init; } =
        new Dictionary<string, long>();
    public IReadOnlyList<MediaTypeStorageAggregate> ByMediaType { get; init; } =
        Array.Empty<MediaTypeStorageAggregate>();
    public IReadOnlyList<VirtualKeyStorageAggregate> TopVirtualKeys { get; init; } =
        Array.Empty<VirtualKeyStorageAggregate>();
}

/// <summary>
/// Aggregate storage usage for one media type.
/// </summary>
public sealed record MediaTypeStorageAggregate(
    string MediaType,
    int FileCount,
    long SizeBytes);

/// <summary>
/// Aggregate storage usage for one virtual key.
/// </summary>
public sealed record VirtualKeyStorageAggregate(
    int VirtualKeyId,
    long SizeBytes);
