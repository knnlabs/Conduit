namespace ConduitLLM.Core.Events
{
    /// <summary>
    /// Event requesting the scheduler to trigger retention checks for all groups.
    /// Published by the distributed scheduler service periodically.
    /// </summary>
    public record MediaCleanupScheduleRequested(
        DateTime ScheduledAt,
        string SchedulerId // Instance ID of the scheduler that triggered this
    ) : DomainEvent
    {
        /// <summary>
        /// Indicates if this is a dry run (no actual deletions).
        /// </summary>
        public bool IsDryRun { get; init; } = false;

        /// <summary>
        /// Optional list of specific group IDs to process.
        /// Null means process all active groups.
        /// </summary>
        public List<int>? TargetGroupIds { get; init; }
    }
}