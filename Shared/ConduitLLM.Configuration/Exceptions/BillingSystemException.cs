using System;

namespace ConduitLLM.Configuration.Exceptions
{
    /// <summary>
    /// Exception thrown when billing system encounters a critical failure
    /// </summary>
    public class BillingSystemException : Exception
    {
        /// <summary>
        /// Gets the Virtual Key ID associated with the billing failure, if available
        /// </summary>
        public int? VirtualKeyId { get; }

        /// <summary>
        /// Gets the error code for categorizing the billing failure
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// Initializes a new instance of the BillingSystemException class
        /// </summary>
        /// <param name="message">The error message</param>
        public BillingSystemException(string message)
            : base(message)
        {
            ErrorCode = "BILLING_SYSTEM_ERROR";
        }

        /// <summary>
        /// Initializes a new instance of the BillingSystemException class with an inner exception
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="innerException">The inner exception</param>
        public BillingSystemException(string message, Exception innerException)
            : base(message, innerException)
        {
            ErrorCode = "BILLING_SYSTEM_ERROR";
        }

        /// <summary>
        /// Initializes a new instance of the BillingSystemException class with additional context
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="virtualKeyId">The Virtual Key ID associated with the failure</param>
        /// <param name="errorCode">A specific error code for categorization</param>
        /// <param name="innerException">The inner exception</param>
        public BillingSystemException(string message, int? virtualKeyId, string errorCode, Exception? innerException = null)
            : base(message, innerException)
        {
            VirtualKeyId = virtualKeyId;
            ErrorCode = errorCode;
        }

        /// <summary>
        /// Common error codes for billing system failures
        /// </summary>
        public static class ErrorCodes
        {
            public const string RedisConnectionFailed = "REDIS_CONNECTION_FAILED";
            public const string RedisUpdateFailed = "REDIS_UPDATE_FAILED";
            public const string DatabaseUpdateFailed = "DATABASE_UPDATE_FAILED";
            public const string InvalidConfiguration = "INVALID_CONFIGURATION";
            public const string RateLimitExceeded = "RATE_LIMIT_EXCEEDED";
            public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";
        }
    }
}