namespace ConduitLLM.Configuration.DTOs.BatchOperations
{
    /// <summary>
    /// Current status of a batch operation
    /// </summary>
    public class BatchOperationStatusResponse
    {
        /// <summary>
        /// Operation identifier
        /// </summary>
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// Type of operation
        /// </summary>
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// Current status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Total items in batch
        /// </summary>
        public int TotalItems { get; set; }

        /// <summary>
        /// Items processed so far
        /// </summary>
        public int ProcessedCount { get; set; }

        /// <summary>
        /// Successful items
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Failed items
        /// </summary>
        public int FailedCount { get; set; }

        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public int ProgressPercentage { get; set; }

        /// <summary>
        /// Time elapsed
        /// </summary>
        public TimeSpan ElapsedTime { get; set; }

        /// <summary>
        /// Estimated time remaining
        /// </summary>
        public TimeSpan EstimatedTimeRemaining { get; set; }

        /// <summary>
        /// Processing rate
        /// </summary>
        public double ItemsPerSecond { get; set; }

        /// <summary>
        /// Current item being processed
        /// </summary>
        public string? CurrentItem { get; set; }

        /// <summary>
        /// Whether operation can be cancelled
        /// </summary>
        public bool CanCancel { get; set; }
    }
}