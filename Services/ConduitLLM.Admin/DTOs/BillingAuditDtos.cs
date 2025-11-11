using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Admin.DTOs
{
    /// <summary>
    /// Request DTO for querying billing audit events
    /// </summary>
    public class BillingAuditQueryRequest
    {
        /// <summary>
        /// Start date for the query (inclusive)
        /// </summary>
        public DateTime From { get; set; }

        /// <summary>
        /// End date for the query (inclusive)
        /// </summary>
        public DateTime To { get; set; }

        /// <summary>
        /// Optional event type filter
        /// </summary>
        public BillingAuditEventType? EventType { get; set; }

        /// <summary>
        /// Optional virtual key ID filter
        /// </summary>
        public int? VirtualKeyId { get; set; }

        /// <summary>
        /// Page number (1-based)
        /// </summary>
        public int PageNumber { get; set; } = 1;

        /// <summary>
        /// Page size (max 1000)
        /// </summary>
        public int PageSize { get; set; } = 100;
    }

    /// <summary>
    /// Response DTO for billing audit event queries
    /// </summary>
    public class BillingAuditResponse
    {
        /// <summary>
        /// List of audit events
        /// </summary>
        public List<BillingAuditEventDto> Events { get; set; } = new();

        /// <summary>
        /// Total count of matching events
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Current page number
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// Page size
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Total pages available
        /// </summary>
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    }

    /// <summary>
    /// DTO for billing audit event
    /// </summary>
    public class BillingAuditEventDto
    {
        /// <summary>
        /// Unique identifier for the audit event
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Timestamp when the event occurred
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Type of billing event
        /// </summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>
        /// Virtual key ID if applicable
        /// </summary>
        public int? VirtualKeyId { get; set; }

        /// <summary>
        /// Virtual key name for display
        /// </summary>
        public string? VirtualKeyName { get; set; }

        /// <summary>
        /// Model name
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// Request identifier
        /// </summary>
        public string? RequestId { get; set; }

        /// <summary>
        /// Calculated cost
        /// </summary>
        public decimal? CalculatedCost { get; set; }

        /// <summary>
        /// Failure reason if applicable
        /// </summary>
        public string? FailureReason { get; set; }

        /// <summary>
        /// Provider type
        /// </summary>
        public string? ProviderType { get; set; }

        /// <summary>
        /// HTTP status code
        /// </summary>
        public int? HttpStatusCode { get; set; }

        /// <summary>
        /// Request path
        /// </summary>
        public string? RequestPath { get; set; }

        /// <summary>
        /// Whether cost was estimated
        /// </summary>
        public bool IsEstimated { get; set; }

        /// <summary>
        /// Usage details (parsed from JSON)
        /// </summary>
        public UsageDto? Usage { get; set; }

        /// <summary>
        /// Additional metadata
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// DTO for usage data
    /// </summary>
    public class UsageDto
    {
        /// <summary>
        /// Prompt/input tokens
        /// </summary>
        public int? PromptTokens { get; set; }

        /// <summary>
        /// Completion/output tokens
        /// </summary>
        public int? CompletionTokens { get; set; }

        /// <summary>
        /// Total tokens
        /// </summary>
        public int? TotalTokens { get; set; }

        /// <summary>
        /// Image count for image generation
        /// </summary>
        public int? ImageCount { get; set; }
    }

    /// <summary>
    /// Request DTO for exporting audit events
    /// </summary>
    public class BillingAuditExportRequest
    {
        /// <summary>
        /// Start date for export
        /// </summary>
        public DateTime From { get; set; }

        /// <summary>
        /// End date for export
        /// </summary>
        public DateTime To { get; set; }

        /// <summary>
        /// Export format
        /// </summary>
        public ExportFormat Format { get; set; } = ExportFormat.Json;

        /// <summary>
        /// Optional virtual key ID filter
        /// </summary>
        public int? VirtualKeyId { get; set; }

        /// <summary>
        /// Optional event type filter
        /// </summary>
        public BillingAuditEventType? EventType { get; set; }
    }

    /// <summary>
    /// Export format options
    /// </summary>
    public enum ExportFormat
    {
        /// <summary>
        /// JSON format
        /// </summary>
        Json = 1,

        /// <summary>
        /// CSV format
        /// </summary>
        Csv = 2,

        /// <summary>
        /// Excel format
        /// </summary>
        Excel = 3
    }
}