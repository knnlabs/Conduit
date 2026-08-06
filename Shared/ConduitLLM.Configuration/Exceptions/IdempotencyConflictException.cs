namespace ConduitLLM.Configuration.Exceptions;

/// <summary>
/// Raised when an idempotency key is reused for a different business operation.
/// </summary>
public sealed class IdempotencyConflictException : InvalidOperationException
{
    public IdempotencyConflictException(string message) : base(message)
    {
    }
}
