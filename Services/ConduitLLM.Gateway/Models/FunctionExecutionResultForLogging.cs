namespace ConduitLLM.Gateway.Models;

/// <summary>Function execution details retained for request accounting and logging.</summary>
public sealed class FunctionExecutionResultForLogging
{
    public string? ToolCallId { get; set; }
    public string? FunctionName { get; set; }
    public string? Status { get; set; }
    public decimal? Cost { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? FunctionExecutionId { get; set; }
}
