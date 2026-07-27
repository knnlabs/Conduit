using ConduitLLM.Core.Models.SignalR;

namespace ConduitLLM.Gateway.Models;

/// <summary>
/// Transitional reader for task-progress messages written with the legacy Core discriminator.
/// New messages must use <see cref="TaskProgressMessage"/>.
/// </summary>
internal sealed class LegacyCoreTaskProgressMessage : SignalRMessage
{
    public string TaskId { get; set; } = string.Empty;
    public int ProgressPercentage { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public DateTime? EstimatedCompletionTime { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}
