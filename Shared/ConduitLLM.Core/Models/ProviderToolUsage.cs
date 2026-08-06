namespace ConduitLLM.Core.Models;

/// <summary>
/// Provider-hosted tool usage carried independently of the serialized client response.
/// </summary>
public sealed class ProviderToolUsage
{
    public List<ProviderToolUsageItem> Tools { get; init; } = [];
}

public sealed class ProviderToolUsageItem
{
    public required string ToolName { get; init; }
    public int Count { get; init; }
    public decimal? DurationSeconds { get; init; }
}
