namespace ConduitLLM.Configuration.Entities;

/// <summary>
/// Determines how generation behaves when a media storage quota would be exceeded.
/// </summary>
public enum MediaQuotaExceededBehavior
{
    /// <summary>
    /// Reject the write before it reaches object storage.
    /// </summary>
    Reject = 0,

    /// <summary>
    /// Allow the write and let the scheduled quota cleanup evict old media.
    /// </summary>
    AllowAndEvict = 1
}
