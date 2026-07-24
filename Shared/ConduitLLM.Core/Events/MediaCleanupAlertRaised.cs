namespace ConduitLLM.Core.Events;

/// <summary>
/// Operational media-cleanup alert published by the Admin service and consumed by the
/// Gateway health-monitoring alert pipeline.
/// </summary>
public record MediaCleanupAlertRaised : DomainEvent
{
    /// <summary>
    /// Stable alert kind used to select severity and deduplicate notifications.
    /// </summary>
    public MediaCleanupAlertKind Kind { get; init; }

    /// <summary>
    /// Cleanup phase that produced the alert.
    /// </summary>
    public string CleanupType { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable operation outcome.
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Source that triggered the cleanup operation.
    /// </summary>
    public string TriggeredBy { get; init; } = string.Empty;

    /// <summary>
    /// Admin cleanup leader that observed the condition.
    /// </summary>
    public string LeaderInstanceId { get; init; } = string.Empty;

    /// <summary>
    /// Current monthly delete count when the alert concerns the budget.
    /// </summary>
    public long? MonthlyDeleteCount { get; init; }

    /// <summary>
    /// Configured monthly delete limit when the alert concerns the budget.
    /// </summary>
    public int? MonthlyDeleteBudget { get; init; }

    /// <summary>
    /// Percentage of the monthly delete budget consumed.
    /// </summary>
    public double? BudgetUsedPercent { get; init; }
}

/// <summary>
/// Media cleanup conditions that require an operational notification.
/// </summary>
public enum MediaCleanupAlertKind
{
    OperationFailure,
    BudgetThreshold
}
