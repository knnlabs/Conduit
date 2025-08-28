namespace ConduitLLM.Core.Events
{
    /// <summary>
    /// Event confirming successful deletion of media files from storage.
    /// Used for audit logging and metrics tracking.
    /// </summary>
    public record MediaDeleted(
        List<string> StorageKeys,
        int VirtualKeyGroupId,
        long BytesFreed,
        DateTime DeletedAt
    ) : DomainEvent
    {
        /// <summary>
        /// Number of files deleted.
        /// </summary>
        public int FilesDeleted => StorageKeys?.Count ?? 0;

        /// <summary>
        /// Average file size in bytes.
        /// </summary>
        public long AverageFileSize => FilesDeleted > 0 ? BytesFreed / FilesDeleted : 0;
    }
}