using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces;

/// <summary>
/// Service for auditing pricing rule evaluations.
/// Provides batched writes and querying for billing disputes.
/// </summary>
public interface IPricingAuditService
{
    /// <summary>
    /// Logs a pricing audit event asynchronously.
    /// </summary>
    /// <param name="auditEvent">The audit event to log.</param>
    /// <returns>Task representing the async operation.</returns>
    Task LogAsync(PricingAuditEvent auditEvent);

    /// <summary>
    /// Logs a pricing audit event without waiting (fire-and-forget).
    /// </summary>
    /// <param name="auditEvent">The audit event to log.</param>
    void Log(PricingAuditEvent auditEvent);

    /// <summary>
    /// Gets audit events with optional filtering.
    /// </summary>
    /// <param name="from">Start date (inclusive).</param>
    /// <param name="to">End date (inclusive).</param>
    /// <param name="virtualKeyId">Optional virtual key ID filter.</param>
    /// <param name="modelId">Optional model ID filter.</param>
    /// <param name="pricingType">Optional pricing type filter.</param>
    /// <param name="pageNumber">Page number (1-based).</param>
    /// <param name="pageSize">Page size.</param>
    /// <returns>Paged list of audit events and total count.</returns>
    Task<(List<PricingAuditEvent> Events, int TotalCount)> GetAuditEventsAsync(
        DateTime from,
        DateTime to,
        int? virtualKeyId = null,
        string? modelId = null,
        string? pricingType = null,
        int pageNumber = 1,
        int pageSize = 100);

    /// <summary>
    /// Gets pricing audit events for a specific request.
    /// Used for billing dispute investigations.
    /// </summary>
    /// <param name="requestId">The request ID to look up.</param>
    /// <returns>List of audit events for the request.</returns>
    Task<List<PricingAuditEvent>> GetByRequestIdAsync(string requestId);

    /// <summary>
    /// Gets a summary of pricing evaluations for a time period.
    /// </summary>
    /// <param name="from">Start date (inclusive).</param>
    /// <param name="to">End date (inclusive).</param>
    /// <param name="virtualKeyId">Optional virtual key ID filter.</param>
    /// <returns>Summary statistics of pricing evaluations.</returns>
    Task<PricingAuditSummary> GetSummaryAsync(
        DateTime from,
        DateTime to,
        int? virtualKeyId = null);

    /// <summary>
    /// Removes audit events older than the retention period.
    /// </summary>
    /// <returns>Task representing the cleanup operation.</returns>
    Task CleanupOldAuditEventsAsync();
}

/// <summary>
/// Summary of pricing audit events.
/// </summary>
public class PricingAuditSummary
{
    /// <summary>
    /// Total number of pricing evaluations.
    /// </summary>
    public long TotalEvaluations { get; set; }

    /// <summary>
    /// Number of evaluations that used default rate.
    /// </summary>
    public long DefaultRateUsed { get; set; }

    /// <summary>
    /// Number of evaluations that matched a rule.
    /// </summary>
    public long RulesMatched { get; set; }

    /// <summary>
    /// Total revenue from pricing evaluations.
    /// </summary>
    public decimal TotalRevenue { get; set; }

    /// <summary>
    /// Average rate applied.
    /// </summary>
    public decimal AverageRate { get; set; }

    /// <summary>
    /// Breakdown by pricing type.
    /// </summary>
    public Dictionary<string, long> PricingTypeBreakdown { get; set; } = new();

    /// <summary>
    /// Breakdown by model.
    /// </summary>
    public Dictionary<string, long> ModelBreakdown { get; set; } = new();

    /// <summary>
    /// Top matched rules by frequency.
    /// </summary>
    public List<RuleMatchSummary> TopMatchedRules { get; set; } = new();
}

/// <summary>
/// Summary of rule matches.
/// </summary>
public class RuleMatchSummary
{
    /// <summary>
    /// Description of the rule.
    /// </summary>
    public string RuleDescription { get; set; } = string.Empty;

    /// <summary>
    /// Number of times the rule was matched.
    /// </summary>
    public long MatchCount { get; set; }

    /// <summary>
    /// Total revenue from this rule.
    /// </summary>
    public decimal TotalRevenue { get; set; }
}
