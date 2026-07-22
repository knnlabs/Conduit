namespace ConduitLLM.Configuration.Messaging;

/// <summary>
/// Signals that application-level retry handling is exhausted and the current
/// envelope must be moved directly to dead-letter storage.
/// </summary>
public sealed class NonRetryableMessageException(string message) : Exception(message);
