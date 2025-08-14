using System;

namespace ConduitLLM.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when a service is temporarily unavailable or experiencing issues.
    /// Maps to HTTP 503 Service Unavailable status code.
    /// </summary>
    public class ServiceUnavailableException : ConduitException
    {
        /// <summary>
        /// Gets the service name that is unavailable, if specified.
        /// </summary>
        public string? ServiceName { get; }

        /// <summary>
        /// Gets the estimated time until the service is available again, in seconds.
        /// </summary>
        public int? RetryAfterSeconds { get; }

        /// <summary>
        /// Gets the reason for the service unavailability, if known.
        /// </summary>
        public string? Reason { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceUnavailableException"/> class.
        /// </summary>
        /// <param name="message">The error message describing the service unavailability.</param>
        public ServiceUnavailableException(string message) 
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceUnavailableException"/> class with service information.
        /// </summary>
        /// <param name="message">The error message describing the service unavailability.</param>
        /// <param name="serviceName">The name of the unavailable service.</param>
        public ServiceUnavailableException(string message, string serviceName) 
            : base(message)
        {
            ServiceName = serviceName;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceUnavailableException"/> class with retry information.
        /// </summary>
        /// <param name="message">The error message describing the service unavailability.</param>
        /// <param name="serviceName">The name of the unavailable service.</param>
        /// <param name="retryAfterSeconds">The estimated time until the service is available again, in seconds.</param>
        public ServiceUnavailableException(string message, string serviceName, int retryAfterSeconds) 
            : base(message)
        {
            ServiceName = serviceName;
            RetryAfterSeconds = retryAfterSeconds;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceUnavailableException"/> class with full details.
        /// </summary>
        /// <param name="message">The error message describing the service unavailability.</param>
        /// <param name="serviceName">The name of the unavailable service.</param>
        /// <param name="retryAfterSeconds">The estimated time until the service is available again, in seconds.</param>
        /// <param name="reason">The reason for the service unavailability.</param>
        public ServiceUnavailableException(string message, string serviceName, int retryAfterSeconds, string reason) 
            : base(message)
        {
            ServiceName = serviceName;
            RetryAfterSeconds = retryAfterSeconds;
            Reason = reason;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceUnavailableException"/> class with an inner exception.
        /// </summary>
        /// <param name="message">The error message describing the service unavailability.</param>
        /// <param name="innerException">The inner exception.</param>
        public ServiceUnavailableException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }

        // TODO: Future enhancement - Circuit breaker pattern for provider failures
    }
}