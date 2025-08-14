using System;

namespace ConduitLLM.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when a request payload exceeds the maximum allowed size.
    /// Maps to HTTP 413 Payload Too Large status code.
    /// </summary>
    public class PayloadTooLargeException : ConduitException
    {
        /// <summary>
        /// Gets the actual size of the payload in bytes, if known.
        /// </summary>
        public long? PayloadSize { get; }

        /// <summary>
        /// Gets the maximum allowed size in bytes, if known.
        /// </summary>
        public long? MaximumSize { get; }

        /// <summary>
        /// Gets the type of content that exceeded the limit, if applicable.
        /// </summary>
        public string? ContentType { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PayloadTooLargeException"/> class.
        /// </summary>
        /// <param name="message">The error message describing the payload size issue.</param>
        public PayloadTooLargeException(string message) 
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PayloadTooLargeException"/> class with size information.
        /// </summary>
        /// <param name="message">The error message describing the payload size issue.</param>
        /// <param name="payloadSize">The actual size of the payload in bytes.</param>
        /// <param name="maximumSize">The maximum allowed size in bytes.</param>
        public PayloadTooLargeException(string message, long payloadSize, long maximumSize) 
            : base(message)
        {
            PayloadSize = payloadSize;
            MaximumSize = maximumSize;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PayloadTooLargeException"/> class with content type information.
        /// </summary>
        /// <param name="message">The error message describing the payload size issue.</param>
        /// <param name="payloadSize">The actual size of the payload in bytes.</param>
        /// <param name="maximumSize">The maximum allowed size in bytes.</param>
        /// <param name="contentType">The type of content that exceeded the limit.</param>
        public PayloadTooLargeException(string message, long payloadSize, long maximumSize, string contentType) 
            : base(message)
        {
            PayloadSize = payloadSize;
            MaximumSize = maximumSize;
            ContentType = contentType;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PayloadTooLargeException"/> class with an inner exception.
        /// </summary>
        /// <param name="message">The error message describing the payload size issue.</param>
        /// <param name="innerException">The inner exception.</param>
        public PayloadTooLargeException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }
    }
}