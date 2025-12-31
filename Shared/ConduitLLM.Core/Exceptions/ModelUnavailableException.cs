namespace ConduitLLM.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when a requested model is unavailable
    /// </summary>
    public class ModelUnavailableException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ModelUnavailableException"/> class.
        /// </summary>
        public ModelUnavailableException() : base("The requested model is unavailable")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelUnavailableException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public ModelUnavailableException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelUnavailableException"/> class with a specified error message
        /// and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public ModelUnavailableException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
