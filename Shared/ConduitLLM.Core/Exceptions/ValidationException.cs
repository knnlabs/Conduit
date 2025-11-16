namespace ConduitLLM.Core.Exceptions;

/// <summary>
/// Exception thrown when validation fails for model properties or operations.
/// </summary>
public class ValidationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class.
    /// </summary>
    public ValidationException() : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public ValidationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ValidationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
