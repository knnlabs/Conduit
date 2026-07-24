namespace ConduitLLM.Admin.Interfaces;

/// <summary>
/// Reconciles storage objects against MediaRecord tracking rows.
/// </summary>
public interface IMediaReconciliationService
{
    /// <summary>
    /// Reports untracked storage drift and deletes sufficiently old objects through the
    /// shared guarded deletion engine.
    /// </summary>
    Task<MediaDeletionEngineResult> ReconcileAsync(
        MediaDeletionOperationContext operation,
        CancellationToken cancellationToken = default);
}
