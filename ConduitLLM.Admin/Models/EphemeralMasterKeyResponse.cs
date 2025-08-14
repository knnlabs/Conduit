using System;

namespace ConduitLLM.Admin.Models
{
    /// <summary>
    /// Response model for ephemeral master key generation
    /// </summary>
    public class EphemeralMasterKeyResponse
    {
        /// <summary>
        /// The generated ephemeral master key token
        /// </summary>
        public string EphemeralMasterKey { get; set; } = string.Empty;

        /// <summary>
        /// When the key expires
        /// </summary>
        public DateTimeOffset ExpiresAt { get; set; }

        /// <summary>
        /// Number of seconds until the key expires
        /// </summary>
        public int ExpiresInSeconds { get; set; }
    }
}