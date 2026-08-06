namespace ConduitLLM.Core.Exceptions;

/// <summary>
/// Base class for exceptions specific to the ConduitLLM library.
/// </summary>
public class ConduitException : Exception
{
    public ConduitException() { }
    public ConduitException(string message) : base(message) { }
    public ConduitException(string message, Exception innerException) : base(message, innerException) { }
}
