using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Core.Constants;

/// <summary>
/// Constants for task state string representations.
/// These are the lowercase string values that correspond to the TaskState enum.
/// </summary>
public static class TaskStateConstants
{
    /// <summary>
    /// Task has been created but not started.
    /// Corresponds to TaskState.Pending.
    /// </summary>
    public const string Pending = "pending";
    
    /// <summary>
    /// Task is currently being processed.
    /// Corresponds to TaskState.Processing.
    /// </summary>
    public const string Processing = "processing";
    
    /// <summary>
    /// Task completed successfully.
    /// Corresponds to TaskState.Completed.
    /// </summary>
    public const string Completed = "completed";
    
    /// <summary>
    /// Task failed with an error.
    /// Corresponds to TaskState.Failed.
    /// </summary>
    public const string Failed = "failed";
    
    /// <summary>
    /// Task was cancelled.
    /// Corresponds to TaskState.Cancelled.
    /// </summary>
    public const string Cancelled = "cancelled";
    
    /// <summary>
    /// Task has timed out.
    /// Corresponds to TaskState.TimedOut.
    /// </summary>
    public const string TimedOut = "timedout";
    
    /// <summary>
    /// Initial status returned by some endpoints when task is first created.
    /// This is not part of the TaskState enum but is used in API responses.
    /// </summary>
    public const string Queued = "queued";
    
    /// <summary>
    /// Converts a TaskState enum value to its string representation.
    /// </summary>
    public static string FromTaskState(TaskState state)
    {
        return state.ToString().ToLowerInvariant();
    }
    
    /// <summary>
    /// Attempts to parse a string status to a TaskState enum value.
    /// Returns null if the status is not recognized or is "queued".
    /// </summary>
    public static TaskState? ToTaskState(string status)
    {
        return status?.ToLowerInvariant() switch
        {
            Pending => TaskState.Pending,
            Processing => TaskState.Processing,
            Completed => TaskState.Completed,
            Failed => TaskState.Failed,
            Cancelled => TaskState.Cancelled,
            TimedOut => TaskState.TimedOut,
            Queued => TaskState.Pending, // Map queued to pending
            _ => null
        };
    }
}