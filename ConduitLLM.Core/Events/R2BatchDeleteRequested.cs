namespace ConduitLLM.Core.Events
{
    /// <summary>
    /// Event requesting batch deletion of objects from R2 storage.
    /// Partitioned by bucket name to prevent concurrent operations on same bucket.
    /// </summary>
    public record R2BatchDeleteRequested(
        string BucketName,
        List<string> StorageKeys,
        int VirtualKeyGroupId,
        Guid BatchId
    ) : DomainEvent
    {
        /// <summary>
        /// Partition key for ordered processing by bucket.
        /// Prevents concurrent R2 operations on the same bucket.
        /// </summary>
        public string PartitionKey => BucketName;

        /// <summary>
        /// Timestamp when this deletion was requested.
        /// </summary>
        public DateTime RequestedAt { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Number of objects to delete.
        /// </summary>
        public int ObjectCount => StorageKeys?.Count ?? 0;
    }
}