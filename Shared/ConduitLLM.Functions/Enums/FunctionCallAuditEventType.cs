namespace ConduitLLM.Functions.Enums;

/// <summary>
/// Represents the type of audit event for function calling in chat completions.
/// These events track the lifecycle of function calls initiated by LLMs during agentic interactions.
/// </summary>
public enum FunctionCallAuditEventType
{
    /// <summary>
    /// LLM requested a function call (tool_calls in response)
    /// </summary>
    FunctionCallRequested = 1,

    /// <summary>
    /// Function execution started
    /// </summary>
    FunctionCallExecutionStarted = 2,

    /// <summary>
    /// Function execution completed successfully
    /// </summary>
    FunctionCallExecutionCompleted = 3,

    /// <summary>
    /// Function execution failed
    /// </summary>
    FunctionCallExecutionFailed = 4,

    /// <summary>
    /// Cost calculated for the function call
    /// </summary>
    FunctionCallCostCalculated = 5,

    /// <summary>
    /// Function call could not be parsed from LLM response
    /// </summary>
    FunctionCallParseError = 6
}
