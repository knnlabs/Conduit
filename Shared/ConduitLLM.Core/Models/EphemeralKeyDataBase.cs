namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// Base class for ephemeral key data stored in distributed cache
    /// </summary>
    public abstract class EphemeralKeyDataBase
    {
        /// <summary>
        /// The ephemeral key token
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
    }
}
