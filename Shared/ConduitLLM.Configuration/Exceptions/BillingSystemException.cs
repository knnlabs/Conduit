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
            ErrorCode = "billing_system_error";
        }

        /// <summary>
        /// Initializes a new instance of the BillingSystemException class with an inner exception
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="innerException">The inner exception</param>
        public BillingSystemException(string message, Exception innerException)
            : base(message, innerException)
        {
            ErrorCode = "billing_system_error";
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
    }
}
