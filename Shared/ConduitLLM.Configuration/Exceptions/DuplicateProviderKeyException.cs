using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Exceptions
{
    /// <summary>
    /// Exception thrown when attempting to add a duplicate API key for a provider
    /// </summary>
    public class DuplicateProviderKeyException : InvalidOperationException
    {
        /// <summary>
        /// Gets the provider for which the duplicate key was attempted
        /// </summary>
        public Provider Provider { get; }

        /// <summary>
        /// Gets the provider credential ID for which the duplicate key was attempted
        /// </summary>
        public int ProviderId { get; }

        /// <summary>
        /// Initializes a new instance of the DuplicateProviderKeyException class
        /// </summary>
        /// <param name="provider">The provider</param>
        /// <param name="providerId">The provider credential ID</param>
        public DuplicateProviderKeyException(Provider provider, int providerId)
            : base($"An API key with this value already exists for {provider}. Each API key must be unique per provider.")
        {
            Provider = provider;
            ProviderId = providerId;
        }

        /// <summary>
        /// Initializes a new instance of the DuplicateProviderKeyException class with a custom message
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="provider">The provider</param>
        /// <param name="providerId">The provider credential ID</param>
        public DuplicateProviderKeyException(string message, Provider provider, int providerId)
            : base(message)
        {
            Provider = provider;
            ProviderId = providerId;
        }

        /// <summary>
        /// Initializes a new instance of the DuplicateProviderKeyException class with an inner exception
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="innerException">The inner exception</param>
        /// <param name="provider">The provider</param>
        /// <param name="providerId">The provider credential ID</param>
        public DuplicateProviderKeyException(string message, Exception innerException, Provider provider, int providerId)
            : base(message, innerException)
        {
            Provider = provider;
            ProviderId = providerId;
        }
    }
}