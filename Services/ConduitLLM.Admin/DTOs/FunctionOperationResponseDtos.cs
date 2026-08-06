namespace ConduitLLM.Admin.DTOs;

/// <summary>Result returned after testing a function credential.</summary>
public sealed class FunctionCredentialTestResultDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public object? Details { get; set; }
    public double DurationMs { get; set; }
}

/// <summary>Result returned after clearing the function-cost cache.</summary>
public sealed class FunctionCostCacheClearResultDto
{
    public required string Message { get; set; }
}

/// <summary>Result returned after cleaning up old function executions.</summary>
public sealed class FunctionExecutionCleanupResultDto
{
    public int DeletedCount { get; set; }
    public required string Message { get; set; }
}
