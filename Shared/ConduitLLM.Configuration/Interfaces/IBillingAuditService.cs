using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces
{
    /// <summary>
    /// Service for auditing billing events and failures
    /// </summary>
    public interface IBillingAuditService
    {
        /// <summary>
        /// Logs a billing audit event asynchronously
        /// </summary>
        /// <param name="auditEvent">The audit event to log</param>
        /// <returns>Task representing the async operation</returns>
        Task LogBillingEventAsync(BillingAuditEvent auditEvent);

        /// <summary>
        /// Logs a billing audit event without waiting (fire-and-forget)
        /// </summary>
        /// <param name="auditEvent">The audit event to log</param>
        void LogBillingEvent(BillingAuditEvent auditEvent);

        /// <summary>
        /// Gets audit events with optional filtering
        /// </summary>
        /// <param name="from">Start date (inclusive)</param>
        /// <param name="to">End date (inclusive)</param>
        /// <param name="eventType">Optional event type filter</param>
        /// <param name="virtualKeyId">Optional virtual key ID filter</param>
        /// <param name="pageNumber">Page number (1-based)</param>
        /// <param name="pageSize">Page size</param>
        /// <returns>Paged list of audit events and total count</returns>
        Task<(List<BillingAuditEvent> Events, int TotalCount)> GetAuditEventsAsync(
            DateTime from,
            DateTime to,
            BillingAuditEventType? eventType = null,
            int? virtualKeyId = null,
            int pageNumber = 1,
            int pageSize = 100);

        /// <summary>
        /// Gets a summary of audit events for a time period
        /// </summary>
        /// <param name="from">Start date (inclusive)</param>
        /// <param name="to">End date (inclusive)</param>
        /// <param name="virtualKeyId">Optional virtual key ID filter</param>
        /// <returns>Summary statistics of audit events</returns>
        Task<BillingAuditSummary> GetAuditSummaryAsync(
            DateTime from,
            DateTime to,
            int? virtualKeyId = null);

        /// <summary>
        /// Gets potential revenue loss from failed billing events
        /// </summary>
        /// <param name="from">Start date (inclusive)</param>
        /// <param name="to">End date (inclusive)</param>
        /// <returns>Total potential revenue loss in dollars</returns>
        Task<decimal> GetPotentialRevenueLossAsync(DateTime from, DateTime to);

        /// <summary>
        /// Searches for anomalies in billing patterns
        /// </summary>
        /// <param name="from">Start date (inclusive)</param>
        /// <param name="to">End date (inclusive)</param>
        /// <returns>List of detected anomalies</returns>
        Task<List<BillingAnomaly>> DetectAnomaliesAsync(DateTime from, DateTime to);

        /// <summary>
        /// Removes audit events older than the retention period (90 days)
        /// </summary>
        /// <returns>Task representing the cleanup operation</returns>
        Task CleanupOldAuditEventsAsync();
    }

    /// <summary>
    /// Summary of billing audit events
    /// </summary>
    public class BillingAuditSummary
    {
        /// <summary>
        /// Total number of events
        /// </summary>
        public long TotalEvents { get; set; }

        /// <summary>
        /// Number of successful billing events
        /// </summary>
        public long SuccessfulBillings { get; set; }

        /// <summary>
        /// Number of zero-cost events skipped
        /// </summary>
        public long ZeroCostSkipped { get; set; }

        /// <summary>
        /// Number of estimated usage events
        /// </summary>
        public long EstimatedUsages { get; set; }

        /// <summary>
        /// Number of failed spend updates
        /// </summary>
        public long FailedUpdates { get; set; }

        /// <summary>
        /// Number of error responses skipped
        /// </summary>
        public long ErrorResponsesSkipped { get; set; }

        /// <summary>
        /// Number of missing usage data events
        /// </summary>
        public long MissingUsageData { get; set; }

        /// <summary>
        /// Total successfully billed amount
        /// </summary>
        public decimal TotalBilledAmount { get; set; }

        /// <summary>
        /// Total potential revenue loss
        /// </summary>
        public decimal PotentialRevenueLoss { get; set; }

        /// <summary>
        /// Breakdown by event type
        /// </summary>
        public Dictionary<BillingAuditEventType, long> EventTypeBreakdown { get; set; } = new();

        /// <summary>
        /// Breakdown by provider type
        /// </summary>
        public Dictionary<string, long> ProviderTypeBreakdown { get; set; } = new();
    }

    /// <summary>
    /// Represents a detected billing anomaly
    /// </summary>
    public class BillingAnomaly
    {
        /// <summary>
        /// Type of anomaly detected
        /// </summary>
        public string AnomalyType { get; set; } = string.Empty;

        /// <summary>
        /// Description of the anomaly
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Severity level (High, Medium, Low)
        /// </summary>
        public string Severity { get; set; } = string.Empty;

        /// <summary>
        /// Time period when anomaly was detected
        /// </summary>
        public DateTime DetectedAt { get; set; }

        /// <summary>
        /// Affected virtual key IDs
        /// </summary>
        public List<int> AffectedVirtualKeyIds { get; set; } = new();

        /// <summary>
        /// Estimated impact in dollars
        /// </summary>
        public decimal EstimatedImpact { get; set; }

        /// <summary>
        /// Additional context data
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}