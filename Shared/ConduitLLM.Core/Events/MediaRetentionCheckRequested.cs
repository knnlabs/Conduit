namespace ConduitLLM.Core.Events
{
    /// <summary>
    /// Event triggered periodically to evaluate retention policies for a virtual key group.
    /// This event initiates the media cleanup evaluation process.
    /// </summary>
    public record MediaRetentionCheckRequested(
        int VirtualKeyGroupId,
        DateTime RequestedAt,
        string Reason // "Scheduled", "BalanceChanged", "PolicyChanged", "Manual"
    ) : DomainEvent
    {
        /// <summary>
        /// Partition key for ordered processing by virtual key group.
        /// Ensures all retention checks for a group are processed sequentially.
        /// </summary>
        public string PartitionKey => VirtualKeyGroupId.ToString();
    }
}