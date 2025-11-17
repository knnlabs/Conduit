namespace ConduitLLM.Core.Events
{
    /// <summary>
    /// Event containing a batch of media identified for deletion after retention policy evaluation.
    /// Batching improves R2 API efficiency and reduces operation costs.
    /// </summary>
    public record MediaCleanupBatchRequested(
        int VirtualKeyGroupId,
        List<string> StorageKeys,
        string CleanupReason, // "RetentionExpired", "VirtualKeyDeleted", "GroupDeleted", "ManualCleanup"
        DateTime ScheduledFor
    ) : DomainEvent
    {
        /// <summary>
        /// Partition key for ordered processing by virtual key group.
        /// </summary>
        public string PartitionKey => VirtualKeyGroupId.ToString();

        /// <summary>
        /// Unique identifier for this batch operation.
        /// </summary>
        public Guid BatchId { get; init; } = Guid.NewGuid();

        /// <summary>
        /// Number of items in this batch.
        /// </summary>
        public int BatchSize => StorageKeys?.Count ?? 0;
    }
}