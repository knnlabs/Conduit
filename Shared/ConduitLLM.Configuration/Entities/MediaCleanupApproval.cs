namespace ConduitLLM.Configuration.Entities;

/// <summary>
/// Durable approval state for a scheduler-owned media cleanup scope.
/// Candidate identifiers are intentionally not persisted; approved work is re-queried.
/// </summary>
public class MediaCleanupApproval
{
    public Guid Id { get; set; }
    public string CleanupType { get; set; } = string.Empty;
    public int? VirtualKeyGroupId { get; set; }
    public int CandidateCount { get; set; }
    public long CandidateBytes { get; set; }
    public DateTime CutoffUtc { get; set; }
    public string Status { get; set; } = MediaCleanupApprovalStatuses.Pending;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? DecisionAtUtc { get; set; }
    public DateTime? ExecutedAtUtc { get; set; }
    public string? DecidedBy { get; set; }
    public string? ExecutionStatus { get; set; }
    public int FilesDeleted { get; set; }
    public int RecordsTombstoned { get; set; }
    public int Failures { get; set; }
}

/// <summary>
/// Stable database values for cleanup approval state.
/// </summary>
public static class MediaCleanupApprovalStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Completed = "Completed";
}
