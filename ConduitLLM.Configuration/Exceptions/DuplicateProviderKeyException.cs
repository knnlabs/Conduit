using System;

namespace ConduitLLM.Configuration.Exceptions
{
    /// <summary>
    /// Exception thrown when attempting to add a duplicate API key for a provider
    /// </summary>
    public class DuplicateProviderKeyException : InvalidOperationException
    {
        /// <summary>
        /// Gets the provider type for which the duplicate key was attempted
        /// </summary>
        public ProviderType ProviderType { get; }

        /// <summary>
        /// Gets the provider credential ID for which the duplicate key was attempted
        /// </summary>
        public int ProviderCredentialId { get; }

        /// <summary>
        /// Initializes a new instance of the DuplicateProviderKeyException class
        /// </summary>
        /// <param name="providerType">The provider type</param>
        /// <param name="providerCredentialId">The provider credential ID</param>
        public DuplicateProviderKeyException(ProviderType providerType, int providerCredentialId)
            : base($"An API key with this value already exists for {providerType}. Each API key must be unique per provider.")
        {
            ProviderType = providerType;
            ProviderCredentialId = providerCredentialId;
        }

        /// <summary>
        /// Initializes a new instance of the DuplicateProviderKeyException class with a custom message
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="providerType">The provider type</param>
        /// <param name="providerCredentialId">The provider credential ID</param>
        public DuplicateProviderKeyException(string message, ProviderType providerType, int providerCredentialId)
            : base(message)
        {
            ProviderType = providerType;
            ProviderCredentialId = providerCredentialId;
        }

        /// <summary>
        /// Initializes a new instance of the DuplicateProviderKeyException class with an inner exception
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="innerException">The inner exception</param>
        /// <param name="providerType">The provider type</param>
        /// <param name="providerCredentialId">The provider credential ID</param>
        public DuplicateProviderKeyException(string message, Exception innerException, ProviderType providerType, int providerCredentialId)
            : base(message, innerException)
        {
            ProviderType = providerType;
            ProviderCredentialId = providerCredentialId;
        }
    }
}