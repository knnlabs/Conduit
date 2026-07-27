using System.Text.Json.Serialization;

namespace ConduitLLM.Configuration.DTOs.BatchOperations;

/// <summary>Error information for a failed batch item.</summary>
public sealed class BatchItemError
{
    /// <summary>Index of the item in the batch.</summary>
    public int ItemIndex { get; set; }

    /// <summary>Identifier for the failed item.</summary>
    public string? ItemIdentifier { get; set; }

    /// <summary>Error message.</summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Diagnostic stack trace retained for server-side processing but never exposed on API or
    /// SignalR contracts.
    /// </summary>
    [JsonIgnore]
    public string? StackTrace { get; set; }

    /// <summary>Timestamp of the error.</summary>
    public DateTime ErrorTime { get; set; } = DateTime.UtcNow;
}
