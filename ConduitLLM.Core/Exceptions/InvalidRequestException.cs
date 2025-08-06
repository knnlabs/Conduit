using System;

namespace ConduitLLM.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when a request contains invalid parameters or is malformed.
    /// Maps to HTTP 400 Bad Request status code.
    /// </summary>
    public class InvalidRequestException : ConduitException
    {
        /// <summary>
        /// Gets the error code associated with this invalid request.
        /// </summary>
        public string? ErrorCode { get; }

        /// <summary>
        /// Gets the parameter that caused the validation error, if applicable.
        /// </summary>
        public string? Param { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidRequestException"/> class.
        /// </summary>
        /// <param name="message">The error message describing what is invalid.</param>
        public InvalidRequestException(string message) 
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidRequestException"/> class with an error code.
        /// </summary>
        /// <param name="message">The error message describing what is invalid.</param>
        /// <param name="errorCode">The specific error code for this type of invalid request.</param>
        public InvalidRequestException(string message, string errorCode) 
            : base(message)
        {
            ErrorCode = errorCode;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidRequestException"/> class with error code and parameter.
        /// </summary>
        /// <param name="message">The error message describing what is invalid.</param>
        /// <param name="errorCode">The specific error code for this type of invalid request.</param>
        /// <param name="param">The parameter that caused the validation error.</param>
        public InvalidRequestException(string message, string errorCode, string param) 
            : base(message)
        {
            ErrorCode = errorCode;
            Param = param;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidRequestException"/> class with an inner exception.
        /// </summary>
        /// <param name="message">The error message describing what is invalid.</param>
        /// <param name="innerException">The inner exception.</param>
        public InvalidRequestException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }
    }
}