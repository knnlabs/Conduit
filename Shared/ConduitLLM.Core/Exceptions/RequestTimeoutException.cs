namespace ConduitLLM.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when a request times out while waiting for a response.
    /// Maps to HTTP 408 Request Timeout status code.
    /// </summary>
    public class RequestTimeoutException : ConduitException
    {
        /// <summary>
        /// Gets the timeout duration in seconds, if specified.
        /// </summary>
        public int? TimeoutSeconds { get; }

        /// <summary>
        /// Gets the operation that timed out, if specified.
        /// </summary>
        public string? Operation { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestTimeoutException"/> class.
        /// </summary>
        /// <param name="message">The error message describing the timeout.</param>
        public RequestTimeoutException(string message) 
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestTimeoutException"/> class with timeout duration.
        /// </summary>
        /// <param name="message">The error message describing the timeout.</param>
        /// <param name="timeoutSeconds">The timeout duration in seconds.</param>
        public RequestTimeoutException(string message, int timeoutSeconds) 
            : base(message)
        {
            TimeoutSeconds = timeoutSeconds;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestTimeoutException"/> class with operation details.
        /// </summary>
        /// <param name="message">The error message describing the timeout.</param>
        /// <param name="timeoutSeconds">The timeout duration in seconds.</param>
        /// <param name="operation">The operation that timed out.</param>
        public RequestTimeoutException(string message, int timeoutSeconds, string operation) 
            : base(message)
        {
            TimeoutSeconds = timeoutSeconds;
            Operation = operation;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestTimeoutException"/> class with an inner exception.
        /// </summary>
        /// <param name="message">The error message describing the timeout.</param>
        /// <param name="innerException">The inner exception.</param>
        public RequestTimeoutException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }
    }
}