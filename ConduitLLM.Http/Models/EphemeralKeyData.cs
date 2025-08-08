using System;

namespace ConduitLLM.Http.Models
{
    /// <summary>
    /// Represents the data stored in Redis for an ephemeral API key
    /// </summary>
    public class EphemeralKeyData
    {
        /// <summary>
        /// The ephemeral key token
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// The virtual key ID that this ephemeral key is associated with
        /// </summary>
        public int VirtualKeyId { get; set; }

        /// <summary>
        /// When the ephemeral key was created
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// When the ephemeral key expires
        /// </summary>
        public DateTimeOffset ExpiresAt { get; set; }

        /// <summary>
        /// Whether this key has been consumed (used)
        /// </summary>
        public bool IsConsumed { get; set; }

        /// <summary>
        /// Optional metadata about the ephemeral key
        /// </summary>
        public EphemeralKeyMetadata? Metadata { get; set; }

        /// <summary>
        /// The encrypted virtual key value (base64 encoded)
        /// This allows the ephemeral key to be converted back to a regular virtual key authentication
        /// </summary>
        public string? EncryptedVirtualKey { get; set; }
    }

    /// <summary>
    /// Optional metadata for tracking ephemeral key usage
    /// </summary>
    public class EphemeralKeyMetadata
    {
        /// <summary>
        /// IP address that requested the ephemeral key
        /// </summary>
        public string? SourceIP { get; set; }

        /// <summary>
        /// User agent that requested the ephemeral key
        /// </summary>
        public string? UserAgent { get; set; }

        /// <summary>
        /// Purpose or intended use of the ephemeral key
        /// </summary>
        public string? Purpose { get; set; }

        /// <summary>
        /// Request ID for correlation
        /// </summary>
        public string? RequestId { get; set; }
    }

    /// <summary>
    /// Response when creating an ephemeral key
    /// </summary>
    public class EphemeralKeyResponse
    {
        /// <summary>
        /// The ephemeral key token to use for authentication
        /// </summary>
        public string EphemeralKey { get; set; } = string.Empty;

        /// <summary>
        /// When the ephemeral key expires
        /// </summary>
        public DateTimeOffset ExpiresAt { get; set; }

        /// <summary>
        /// The TTL in seconds
        /// </summary>
        public int ExpiresInSeconds { get; set; }
    }
}