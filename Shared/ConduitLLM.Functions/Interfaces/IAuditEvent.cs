namespace ConduitLLM.Functions.Interfaces;

/// <summary>
/// Interface for audit event entities that have a timestamp for retention cleanup.
/// </summary>
public interface IAuditEvent
{
    /// <summary>
    /// The timestamp when the audit event occurred.
    /// Used for data retention cleanup.
    /// </summary>
    DateTime Timestamp { get; set; }
}
