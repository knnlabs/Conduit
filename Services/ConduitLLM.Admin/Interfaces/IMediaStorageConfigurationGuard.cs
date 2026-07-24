namespace ConduitLLM.Admin.Interfaces;

/// <summary>
/// Prevents cleanup from running when persisted media is paired with ephemeral storage.
/// </summary>
public interface IMediaStorageConfigurationGuard
{
    /// <summary>
    /// Gets the resolved media storage backend name.
    /// </summary>
    string StorageBackend { get; }

    /// <summary>
    /// Gets whether cleanup may run with the current storage configuration.
    /// </summary>
    bool IsCleanupAllowed { get; }

    /// <summary>
    /// Revalidates the storage configuration against persisted media.
    /// </summary>
    Task<bool> ValidateAsync(CancellationToken cancellationToken = default);
}
