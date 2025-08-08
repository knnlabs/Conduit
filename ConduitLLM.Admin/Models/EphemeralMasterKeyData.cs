using System;

namespace ConduitLLM.Admin.Models
{
    /// <summary>
    /// Represents data for an ephemeral master key stored in cache
    /// </summary>
    public class EphemeralMasterKeyData
    {
        /// <summary>
        /// The ephemeral master key token
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// When the key was created
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// When the key expires
        /// </summary>
        public DateTimeOffset ExpiresAt { get; set; }

        /// <summary>
        /// Whether the key has been consumed
        /// </summary>
        public bool IsConsumed { get; set; }

        /// <summary>
        /// Flag indicating this is a valid master key token
        /// </summary>
        public bool IsValid { get; set; } = true;
    }
}