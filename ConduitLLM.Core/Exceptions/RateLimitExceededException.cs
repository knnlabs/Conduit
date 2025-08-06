using System;

namespace ConduitLLM.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when a rate limit has been exceeded.
    /// Maps to HTTP 429 Too Many Requests status code.
    /// </summary>
    public class RateLimitExceededException : ConduitException
    {
        /// <summary>
        /// Gets the number of seconds to wait before retrying, if specified.
        /// </summary>
        public int? RetryAfterSeconds { get; }

        /// <summary>
        /// Gets the limit that was exceeded, if known.
        /// </summary>
        public int? Limit { get; }

        /// <summary>
        /// Gets the time window for the limit (e.g., "per minute", "per hour"), if known.
        /// </summary>
        public string? TimeWindow { get; }

        /// <summary>
        /// Gets the type of rate limit that was exceeded (e.g., "requests", "tokens"), if known.
        /// </summary>
        public string? LimitType { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RateLimitExceededException"/> class.
        /// </summary>
        /// <param name="message">The error message describing the rate limit issue.</param>
        public RateLimitExceededException(string message) 
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RateLimitExceededException"/> class with retry information.
        /// </summary>
        /// <param name="message">The error message describing the rate limit issue.</param>
        /// <param name="retryAfterSeconds">The number of seconds to wait before retrying.</param>
        public RateLimitExceededException(string message, int retryAfterSeconds) 
            : base(message)
        {
            RetryAfterSeconds = retryAfterSeconds;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RateLimitExceededException"/> class with limit details.
        /// </summary>
        /// <param name="message">The error message describing the rate limit issue.</param>
        /// <param name="retryAfterSeconds">The number of seconds to wait before retrying.</param>
        /// <param name="limit">The limit that was exceeded.</param>
        /// <param name="timeWindow">The time window for the limit.</param>
        public RateLimitExceededException(string message, int retryAfterSeconds, int limit, string timeWindow) 
            : base(message)
        {
            RetryAfterSeconds = retryAfterSeconds;
            Limit = limit;
            TimeWindow = timeWindow;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RateLimitExceededException"/> class with full details.
        /// </summary>
        /// <param name="message">The error message describing the rate limit issue.</param>
        /// <param name="retryAfterSeconds">The number of seconds to wait before retrying.</param>
        /// <param name="limit">The limit that was exceeded.</param>
        /// <param name="timeWindow">The time window for the limit.</param>
        /// <param name="limitType">The type of rate limit that was exceeded.</param>
        public RateLimitExceededException(string message, int retryAfterSeconds, int limit, string timeWindow, string limitType) 
            : base(message)
        {
            RetryAfterSeconds = retryAfterSeconds;
            Limit = limit;
            TimeWindow = timeWindow;
            LimitType = limitType;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RateLimitExceededException"/> class with an inner exception.
        /// </summary>
        /// <param name="message">The error message describing the rate limit issue.</param>
        /// <param name="innerException">The inner exception.</param>
        public RateLimitExceededException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }
    }
}