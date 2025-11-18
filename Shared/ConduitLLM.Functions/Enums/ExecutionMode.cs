namespace ConduitLLM.Functions.Enums;

/// <summary>
/// Defines how a function execution is processed.
/// </summary>
public enum ExecutionMode
{
    /// <summary>
    /// Synchronous execution - Conduit waits for the function to complete before returning
    /// </summary>
    Synchronous = 1,

    /// <summary>
    /// Asynchronous execution - Conduit submits the task and returns immediately with an execution ID.
    /// Client can poll for status or receive webhook notification when complete.
    /// </summary>
    Asynchronous = 2
}
