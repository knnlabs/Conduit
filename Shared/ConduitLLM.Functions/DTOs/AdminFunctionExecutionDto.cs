using ConduitLLM.Functions.Enums;

namespace ConduitLLM.Functions.DTOs;

/// <summary>
/// Admin view of a function execution. The shared execution fields remain on the base resource;
/// operational details are isolated under <see cref="Admin"/>.
/// </summary>
public class AdminFunctionExecutionDto : FunctionExecutionDto
{
    /// <summary>
    /// Admin-only execution and delivery diagnostics.
    /// </summary>
    public FunctionExecutionAdminDetailsDto Admin { get; set; } = new();
}

/// <summary>
/// Operational details that are intentionally excluded from the Gateway contract.
/// </summary>
public class FunctionExecutionAdminDetailsDto
{
    public int VirtualKeyId { get; set; }
    public ExecutionMode ExecutionMode { get; set; }
    public int RetryCount { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public string? LeasedBy { get; set; }
    public DateTime? LeaseExpiresAt { get; set; }
    public int Version { get; set; }
    public string? WebhookUrl { get; set; }
    public bool WebhookDelivered { get; set; }
    public int? ProgressPercentage { get; set; }
    public string? StatusMessage { get; set; }
}
