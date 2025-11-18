namespace ConduitLLM.Functions.Enums;

/// <summary>
/// Defines the types of audit events tracked for function executions.
/// Used for compliance, billing verification, and troubleshooting.
/// </summary>
public enum FunctionAuditEventType
{
    /// <summary>
    /// Function execution was requested
    /// </summary>
    ExecutionRequested = 1,

    /// <summary>
    /// Execution started processing
    /// </summary>
    ExecutionStarted = 2,

    /// <summary>
    /// Progress update during execution
    /// </summary>
    ExecutionProgress = 3,

    /// <summary>
    /// Execution completed successfully
    /// </summary>
    ExecutionCompleted = 4,

    /// <summary>
    /// Execution failed
    /// </summary>
    ExecutionFailed = 5,

    /// <summary>
    /// Cost was calculated for the execution
    /// </summary>
    CostCalculated = 6,

    /// <summary>
    /// Virtual key group balance was updated (spend recorded)
    /// </summary>
    SpendUpdated = 7,

    /// <summary>
    /// Failed to update spend (balance update error)
    /// </summary>
    SpendUpdateFailed = 8,

    /// <summary>
    /// Retry was scheduled for failed execution
    /// </summary>
    RetryScheduled = 9,

    /// <summary>
    /// Execution was cancelled
    /// </summary>
    ExecutionCancelled = 10,

    /// <summary>
    /// Execution timed out
    /// </summary>
    ExecutionTimedOut = 11
}
