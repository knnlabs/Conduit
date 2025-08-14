using System;

namespace ConduitLLM.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when authorization fails for accessing a resource or performing an operation.
    /// Maps to HTTP 403 Forbidden status code.
    /// </summary>
    public class AuthorizationException : ConduitException
    {
        /// <summary>
        /// Gets the resource that was attempted to be accessed, if applicable.
        /// </summary>
        public string? Resource { get; }

        /// <summary>
        /// Gets the action that was attempted, if applicable.
        /// </summary>
        public string? Action { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthorizationException"/> class.
        /// </summary>
        /// <param name="message">The error message describing the authorization failure.</param>
        public AuthorizationException(string message) 
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthorizationException"/> class with resource information.
        /// </summary>
        /// <param name="message">The error message describing the authorization failure.</param>
        /// <param name="resource">The resource that was attempted to be accessed.</param>
        public AuthorizationException(string message, string resource) 
            : base(message)
        {
            Resource = resource;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthorizationException"/> class with resource and action information.
        /// </summary>
        /// <param name="message">The error message describing the authorization failure.</param>
        /// <param name="resource">The resource that was attempted to be accessed.</param>
        /// <param name="action">The action that was attempted.</param>
        public AuthorizationException(string message, string resource, string action) 
            : base(message)
        {
            Resource = resource;
            Action = action;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthorizationException"/> class with an inner exception.
        /// </summary>
        /// <param name="message">The error message describing the authorization failure.</param>
        /// <param name="innerException">The inner exception.</param>
        public AuthorizationException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }
    }
}