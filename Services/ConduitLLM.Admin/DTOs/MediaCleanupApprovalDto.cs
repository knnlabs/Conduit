using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Admin.DTOs;

/// <summary>
/// Admin-facing snapshot of a large cleanup approval.
/// </summary>
public sealed class MediaCleanupApprovalDto
{
    public Guid Id { get; set; }
    public string CleanupType { get; set; } = string.Empty;
    public int? VirtualKeyGroupId { get; set; }
    public int CandidateCount { get; set; }
    public long CandidateBytes { get; set; }
    public DateTime CutoffUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string? ExecutionStatus { get; set; }

    public static MediaCleanupApprovalDto FromEntity(MediaCleanupApproval approval) => new()
    {
        Id = approval.Id,
        CleanupType = approval.CleanupType,
        VirtualKeyGroupId = approval.VirtualKeyGroupId,
        CandidateCount = approval.CandidateCount,
        CandidateBytes = approval.CandidateBytes,
        CutoffUtc = approval.CutoffUtc,
        Status = approval.Status,
        CreatedAtUtc = approval.CreatedAtUtc,
        UpdatedAtUtc = approval.UpdatedAtUtc,
        ExpiresAtUtc = approval.ExpiresAtUtc,
        ExecutionStatus = approval.ExecutionStatus
    };
}

/// <summary>
/// Result of approving or rejecting a cleanup request.
/// </summary>
public sealed class MediaCleanupApprovalActionDto
{
    public MediaCleanupApprovalDto Approval { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}
