namespace ConduitLLM.Functions.Enums;

/// <summary>
/// Defines the current state of a function execution.
/// </summary>
public enum ExecutionState
{
    /// <summary>
    /// Execution has been submitted but not yet started
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Execution is currently in progress
    /// </summary>
    Running = 2,

    /// <summary>
    /// Execution completed successfully
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Execution failed due to an error
    /// </summary>
    Failed = 4,

    /// <summary>
    /// Execution was cancelled by user or system
    /// </summary>
    Cancelled = 5,

    /// <summary>
    /// Execution exceeded the configured timeout period
    /// </summary>
    TimedOut = 6
}
