namespace ConduitLLM.Core.Events
{
    /// <summary>
    /// Event to schedule a progress check for a video generation task.
    /// </summary>
    public record VideoProgressCheckRequested : DomainEvent
    {
        /// <summary>
        /// The ID of the video generation request.
        /// </summary>
        public string RequestId { get; init; } = string.Empty;
        
        /// <summary>
        /// The virtual key ID for partitioning.
        /// </summary>
        public string VirtualKeyId { get; init; } = string.Empty;
        
        /// <summary>
        /// The scheduled check time.
        /// </summary>
        public DateTime ScheduledAt { get; init; }
        
        /// <summary>
        /// The progress check interval number (0-based).
        /// </summary>
        public int IntervalIndex { get; init; }
        
        /// <summary>
        /// Total intervals expected.
        /// </summary>
        public int TotalIntervals { get; init; }
        
        /// <summary>
        /// Start time of the video generation.
        /// </summary>
        public DateTime StartTime { get; init; }
        
        /// <summary>
        /// Partition key for ordered processing.
        /// </summary>
        public string PartitionKey => VirtualKeyId;
    }
    
    /// <summary>
    /// Event to cancel progress tracking for a video generation task.
    /// </summary>
    public record VideoProgressTrackingCancelled : DomainEvent
    {
        /// <summary>
        /// The ID of the video generation request.
        /// </summary>
        public string RequestId { get; init; } = string.Empty;
        
        /// <summary>
        /// The virtual key ID for partitioning.
        /// </summary>
        public string VirtualKeyId { get; init; } = string.Empty;
        
        /// <summary>
        /// Reason for cancellation.
        /// </summary>
        public string Reason { get; init; } = string.Empty;
        
        /// <summary>
        /// Partition key for ordered processing.
        /// </summary>
        public string PartitionKey => VirtualKeyId;
    }
}