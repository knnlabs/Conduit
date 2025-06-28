namespace ConduitLLM.WebUI.Constants;

/// <summary>
/// Constants for task status values used in the WebUI.
/// These correspond to the lowercase string representation of the TaskState enum.
/// </summary>
public static class TaskStatusConstants
{
    /// <summary>
    /// Task has been created but not started.
    /// </summary>
    public const string Pending = "pending";
    
    /// <summary>
    /// Task is currently being processed.
    /// </summary>
    public const string Processing = "processing";
    
    /// <summary>
    /// Task completed successfully.
    /// </summary>
    public const string Completed = "completed";
    
    /// <summary>
    /// Task failed with an error.
    /// </summary>
    public const string Failed = "failed";
    
    /// <summary>
    /// Task was cancelled.
    /// </summary>
    public const string Cancelled = "cancelled";
    
    /// <summary>
    /// Task has timed out.
    /// </summary>
    public const string TimedOut = "timedout";
    
    /// <summary>
    /// Initial status when task is first created (used by some endpoints).
    /// </summary>
    public const string Queued = "queued";
    
    /// <summary>
    /// Checks if a status indicates the task is still active (not completed/failed/cancelled).
    /// </summary>
    public static bool IsActiveStatus(string status)
    {
        return status == Pending || status == Processing || status == Queued;
    }
    
    /// <summary>
    /// Checks if a status indicates the task has finished (successfully or not).
    /// </summary>
    public static bool IsTerminalStatus(string status)
    {
        return status == Completed || status == Failed || status == Cancelled || status == TimedOut;
    }
}