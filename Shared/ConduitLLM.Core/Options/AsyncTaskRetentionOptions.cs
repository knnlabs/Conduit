namespace ConduitLLM.Core.Options;

/// <summary>Configuration for automatic async-task retention.</summary>
public sealed class AsyncTaskRetentionOptions
{
    public const string SectionName = "AsyncTaskRetention";

    public bool Enabled { get; set; } = true;
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan ArchiveCompletedAfter { get; set; } = TimeSpan.FromHours(24);
    public TimeSpan DeleteArchivedAfter { get; set; } = TimeSpan.FromDays(30);
    public TimeSpan ArchiveStaleAfter { get; set; } = TimeSpan.FromDays(7);
    public int BatchSize { get; set; } = 500;
}
