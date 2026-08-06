using ConduitLLM.Admin.DTOs;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Admin.Interfaces;

/// <summary>
/// Persists and evaluates approvals for scheduler-owned large cleanup scopes.
/// </summary>
public interface IMediaCleanupApprovalService
{
    Task<MediaCleanupApproval?> GetActiveApprovalAsync(
        string cleanupType,
        int? virtualKeyGroupId,
        CancellationToken cancellationToken = default);

    Task<MediaCleanupApproval> CreateOrRefreshPendingAsync(
        string cleanupType,
        int? virtualKeyGroupId,
        int candidateCount,
        long candidateBytes,
        DateTime cutoffUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MediaCleanupApprovalDto>> ListPendingAsync(
        CancellationToken cancellationToken = default);

    Task<MediaCleanupApproval?> ApproveAsync(
        Guid id,
        string decidedBy,
        CancellationToken cancellationToken = default);

    Task<MediaCleanupApproval?> RejectAsync(
        Guid id,
        string decidedBy,
        CancellationToken cancellationToken = default);

    Task RecordExecutionAsync(
        Guid id,
        MediaDeletionEngineResult result,
        bool isFinalPage = true,
        CancellationToken cancellationToken = default);

    Task CompleteEmptyApprovalAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
