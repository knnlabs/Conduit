using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.DTOs.BatchOperations
{
    /// <summary>
    /// Response when starting a batch operation
    /// </summary>
    public class BatchOperationStartResponse
    {
        /// <summary>
        /// Unique identifier for tracking the operation
        /// </summary>
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// Type of batch operation
        /// </summary>
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// Total number of items in the batch
        /// </summary>
        public int TotalItems { get; set; }

        /// <summary>
        /// URL to check operation status
        /// </summary>
        public string StatusUrl { get; set; } = string.Empty;

        /// <summary>
        /// SignalR task ID to subscribe to for real-time updates
        /// </summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// SignalR event names to listen for
        /// </summary>
        public List<string> SignalREvents { get; set; } = new()
        {
            "BatchOperationStarted",
            "BatchOperationProgress",
            "BatchOperationCompleted",
            "BatchOperationFailed",
            "BatchOperationCancelled",
            "BatchItemCompleted"
        };

        /// <summary>
        /// Informational message
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}