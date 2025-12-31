using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Enums;

namespace ConduitLLM.Functions.Interfaces;

/// <summary>
/// Service for logging and querying function call audit events.
/// Follows the same pattern as BillingAuditService with batch processing.
/// </summary>
public interface IFunctionCallAuditService
{
    /// <summary>
    /// Logs a function call audit event (fire-and-forget, non-blocking).
    /// Events are queued and flushed in batches.
    /// </summary>
    /// <param name="auditEvent">The audit event to log</param>
    void LogFunctionCallEvent(FunctionCallAudit auditEvent);

    /// <summary>
    /// Logs a function call audit event asynchronously (waits for flush if batch is full).
    /// </summary>
    /// <param name="auditEvent">The audit event to log</param>
    Task LogFunctionCallEventAsync(FunctionCallAudit auditEvent);

    /// <summary>
    /// Forces a flush of all pending audit events to the database.
    /// </summary>
    Task FlushEventsAsync();

    /// <summary>
    /// Gets function call audit events with filtering and pagination.
    /// </summary>
    /// <param name="from">Start date (inclusive)</param>
    /// <param name="to">End date (inclusive)</param>
    /// <param name="eventType">Optional event type filter</param>
    /// <param name="virtualKeyId">Optional virtual key filter</param>
    /// <param name="functionConfigurationId">Optional function configuration filter</param>
    /// <param name="chatCompletionId">Optional chat completion filter</param>
    /// <param name="pageNumber">Page number (1-indexed)</param>
    /// <param name="pageSize">Page size</param>
    /// <returns>Tuple of events and total count</returns>
    Task<(List<FunctionCallAudit> Events, int TotalCount)> GetAuditEventsAsync(
        DateTime from,
        DateTime to,
        FunctionCallAuditEventType? eventType = null,
        int? virtualKeyId = null,
        int? functionConfigurationId = null,
        Guid? chatCompletionId = null,
        int pageNumber = 1,
        int pageSize = 100);

    /// <summary>
    /// Gets summary statistics for function calls in a date range.
    /// </summary>
    /// <param name="from">Start date (inclusive)</param>
    /// <param name="to">End date (inclusive)</param>
    /// <param name="virtualKeyId">Optional virtual key filter</param>
    /// <returns>Summary statistics</returns>
    Task<FunctionCallAuditSummary> GetAuditSummaryAsync(
        DateTime from,
        DateTime to,
        int? virtualKeyId = null);
}

/// <summary>
/// Summary statistics for function call audits.
/// </summary>
public class FunctionCallAuditSummary
{
    public int TotalFunctionCalls { get; set; }
    public int SuccessfulCalls { get; set; }
    public int FailedCalls { get; set; }
    public decimal TotalCost { get; set; }
    public Dictionary<string, int> CallsByFunction { get; set; } = new();
    public Dictionary<FunctionCallAuditEventType, int> CallsByEventType { get; set; } = new();
}
