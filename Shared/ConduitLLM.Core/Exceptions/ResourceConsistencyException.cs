namespace ConduitLLM.Core.Exceptions;

/// <summary>
/// Raised when persistence reports success but the newly created resource cannot be read back.
/// This is a server consistency failure, never a client request error.
/// </summary>
public sealed class ResourceConsistencyException : ConduitException
{
    public ResourceConsistencyException(string resourceType, object resourceId)
        : base($"Failed to retrieve newly created {resourceType} with ID {resourceId}.")
    {
    }
}
